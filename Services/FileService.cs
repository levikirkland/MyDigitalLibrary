using MyDigitalLibrary.Data;
using MyDigitalLibrary.Entities;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyDigitalLibrary.Repositories;

namespace MyDigitalLibrary.Services;

public class FileService : IFileService
{
    private readonly IFileRepository _repo;
    private readonly IStorageService _storage;
    private readonly ILogger<FileService> _log;
    public FileService(IFileRepository repo, IStorageService storage, ILogger<FileService> log) { _repo = repo; _storage = storage; _log = log; }

    public async Task<FileEntity?> GetFileByHashAsync(string sha256)
    {
        return await _repo.GetByShaAsync(sha256);
    }

    public async Task<FileEntity?> GetFileByIdAsync(int id)
    {
        return await _repo.GetByIdAsync(id);
    }

    public async Task DecrementRefCountAsync(int fileId)
    {
        var file = await _repo.GetByIdAsync(fileId);
        if (file == null) return;
        file.RefCount -= 1;
        if (file.RefCount <= 0)
        {
            // Delete from storage and DB
            try
            {
                await _storage.DeleteFileAsync(file.StoragePath);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to delete storage for file {FileId} at {Path}", file.Id, file.StoragePath);
            }
            await _repo.DeleteAsync(file);
        }
        else
        {
            await _repo.SaveChangesAsync();
        }
    }

    public async Task<FileEntity> GetOrUploadFileAsync(Stream inputStream, string filename, int userId, string? containerName = null)
    {
        // Read into MemoryStream to compute hash and allow re-use for saving
        using var ms = new MemoryStream();
        await inputStream.CopyToAsync(ms);
        ms.Position = 0;
        // compute sha256
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(ms);
        var shaHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        // Check existing file
        _log.LogInformation("Computed SHA256 {Sha} for file {Filename}", shaHex, filename);
        var existing = await _repo.GetByShaAsync(shaHex);
        if (existing != null)
        {
            // If caller requested a specific container, only reuse an existing file stored in the same container.
            if (!string.IsNullOrWhiteSpace(containerName))
            {
                try
                {
                    var uri = new Uri(existing.StoragePath);
                    var segs = uri.Segments.Select(s => s.Trim('/')).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    var existingContainer = segs.Length > 0 ? segs[0] : string.Empty;
                    if (string.Equals(existingContainer, containerName, StringComparison.OrdinalIgnoreCase))
                    {
                        _log.LogInformation("Found existing file {Id} in same container {Container} for sha {Sha} - increment refcount", existing.Id, existingContainer, shaHex);
                        existing.RefCount += 1;
                        await _repo.SaveChangesAsync();
                        return existing;
                    }
                    // Otherwise fallthrough and upload new file into requested container
                }
                catch
                {
                    // If parsing fails, fallback to conservative behavior and upload new file
                }
            }
            else
            {
                // No container requested -> reuse any existing file
                _log.LogInformation("Found existing file {Id} for sha {Sha} - increment refcount", existing.Id, shaHex);
                existing.RefCount += 1;
                await _repo.SaveChangesAsync();
                return existing;
            }
        }

        // No existing file (or container mismatch), store via storage service
        ms.Position = 0;
        var storagePath = await _storage.SaveFileAsync(ms, filename, userId, containerName);
        var fileEntity = new FileEntity { Sha256 = shaHex, StoragePath = storagePath, Size = ms.Length };
        _log.LogInformation("Saving new FileEntity for sha {Sha} path {Path}", shaHex, storagePath);
        try
        {
            var added = await _repo.AddAsync(fileEntity);
            return added;
        }
        catch
        {
            // If we failed to save DB record, attempt to delete uploaded storage to avoid orphan
            try { await _storage.DeleteFileAsync(storagePath); } catch { }
            throw;
        }
    }
}
