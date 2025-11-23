namespace MyDigitalLibrary.Core.Services;

public class LocalStorageService : IStorageService
{
    private readonly IWebHostEnvironment _env;
    public LocalStorageService(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveFileAsync(Stream inputStream, string filename, int userId, string? containerName = null)
    {
        var userDir = Path.Combine(_env.ContentRootPath, "uploads", userId.ToString());
        Directory.CreateDirectory(userDir);
        var destPath = Path.Combine(userDir, filename);
        using (var outStream = File.Create(destPath)) await inputStream.CopyToAsync(outStream);
        return destPath;
    }

    public Task DeleteFileAsync(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<string> GetDownloadUrlAsync(string storagePath, TimeSpan? expires = null)
    {
        // For local storage, we just return the path. Controller should treat that as a local filesystem path.
        return Task.FromResult(storagePath);
    }

    public Task<Stream> OpenReadAsync(string storagePath)
    {
        // storagePath is a local filesystem path
        Stream s = File.OpenRead(storagePath);
        return Task.FromResult(s);
    }
}
