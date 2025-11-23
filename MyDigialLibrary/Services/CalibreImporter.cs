using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using MyDigitalLibrary.Entities;
using MyDigitalLibrary.Services;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace MyDigitalLibrary.Services;

// Simple importer that scans a Calibre library directory and imports book files and optional covers.
public class CalibreImporter
{
    private readonly IFileService _fileService;
    private readonly IBookService _bookService;
    private readonly IConfiguration _config;
    private readonly ILogger<CalibreImporter> _logger;

    private static readonly string[] BookExtensions = new[] { ".epub", ".mobi", ".azw3", ".pdf", ".fb2", ".rtf" };
    private static readonly string[] PreferredFormats = new[] { "EPUB", "AZW3", "MOBI", "PDF" };

    public CalibreImporter(IFileService fileService, IBookService bookService, IConfiguration config, ILogger<CalibreImporter> logger)
    {
        _fileService = fileService;
        _bookService = bookService;
        _config = config;
        _logger = logger;
    }

    // Import all recognized book files under the calibre library directory for the given user.
    // This is intentionally conservative: if a metadata.db is present we'll use it for accurate metadata and file selection.
    // Optional progress reports (processed, total) can be provided for UI updates.
    public async Task<(int Imported, int Skipped)> ImportFromDirectoryAsync(string calibreRootPath, int userId, bool importCovers = true, CancellationToken cancellation = default, IProgress<(int processed,int total)>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(calibreRootPath)) throw new ArgumentException("Path required", nameof(calibreRootPath));
        if (!Directory.Exists(calibreRootPath)) throw new DirectoryNotFoundException(calibreRootPath);

        var metadataDb = Path.Combine(calibreRootPath, "metadata.db");
        if (File.Exists(metadataDb))
        {
            _logger.LogInformation("Found Calibre metadata.db, importing using database for {Path}", calibreRootPath);
            return await ImportFromMetadataDbAsync(metadataDb, calibreRootPath, userId, importCovers, cancellation, progress);
        }

        // Fallback scanning
        var originalsContainer = _config["AZURE_STORAGE_CONTAINER_ORIGINALS"] ?? "originals";

        var existing = (await _bookService.GetBooksByUserIdAsync(userId)).Select(b => b.OriginalFilename).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0;

        // Calibre stores books under folders per author/title; we scan recursively and pick files by extension
        var files = Directory.EnumerateFiles(calibreRootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => BookExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToArray();

        int total = files.Length;
        int processed = 0;

        foreach (var f in files)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var originalFilename = Path.GetFileName(f);
                if (existing.Contains(originalFilename))
                {
                    skipped++;
                    processed++;
                    progress?.Report((processed, total));
                    continue;
                }

                // Derive metadata from path: assume .../Author/Title/file.ext or .../Title/file.ext
                var rel = Path.GetRelativePath(calibreRootPath, f);
                var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string title = Path.GetFileNameWithoutExtension(f);
                string authors = string.Empty;
                if (parts.Length >= 3)
                {
                    // e.g. Author/Title/file.ext
                    authors = parts[parts.Length - 3];
                    title = parts[parts.Length - 2];
                }
                else if (parts.Length == 2)
                {
                    authors = parts[parts.Length - 2];
                }

                // Upload original file
                await using var stream = File.OpenRead(f);
                var filename = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{originalFilename}";
                var fileEntity = await _fileService.GetOrUploadFileAsync(stream, filename, userId, originalsContainer);

                // Attempt to find cover.jpg in same folder
                string? coverPath = null;
                int? coverFileId = null;
                if (importCovers)
                {
                    try
                    {
                        var folder = Path.GetDirectoryName(f) ?? calibreRootPath;
                        var coverCandidates = new[] { "cover.jpg", "cover.jpeg", "cover.png" };
                        var found = coverCandidates.Select(c => Path.Combine(folder, c)).FirstOrDefault(File.Exists);
                        if (found != null)
                        {
                            await using var cs = File.OpenRead(found);
                            // Compute SHA256 and attempt to reuse existing file by hash
                            cs.Position = 0;
                            var sha = ComputeSha256Hex(cs);
                            var existingCover = await _fileService.GetFileByHashAsync(sha);
                            if (existingCover != null)
                            {
                                coverPath = existingCover.StoragePath;
                                coverFileId = existingCover.Id;
                            }
                            else
                            {
                                cs.Position = 0;
                                var coverName = $"thumb_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(found)}";
                                var coverEntity = await _fileService.GetOrUploadFileAsync(cs, coverName, userId, _config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "cover-thumbnails");
                                coverPath = coverEntity.StoragePath;
                                coverFileId = coverEntity.Id;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Cover upload failed for {File}", f);
                    }
                }

                var info = new FileInfo(f);
                var bookEntity = new BookEntity
                {
                    UserId = userId,
                    Title = title,
                    Authors = authors,
                    OriginalFilename = originalFilename,
                    FilePath = fileEntity.StoragePath,
                    FileSize = fileEntity.Size,
                    MimeType = GetMimeType(originalFilename),
                    CoverPath = coverPath,
                    FileId = fileEntity.Id,
                    CoverFileId = coverFileId,
                    CreatedAt = info.CreationTimeUtc,
                    UpdatedAt = info.LastWriteTimeUtc
                };

                await _bookService.CreateBookAsync(bookEntity);
                existing.Add(originalFilename);
                imported++;
                processed++;
                progress?.Report((processed, total));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import file {File}", f);
                processed++;
                progress?.Report((processed, total));
            }
        }

        return (imported, skipped);
    }

    // Use Calibre metadata.db to import books more accurately and efficiently
    private async Task<(int Imported, int Skipped)> ImportFromMetadataDbAsync(string metadataDbPath, string calibreRootPath, int userId, bool importCovers, CancellationToken cancellation, IProgress<(int processed,int total)>? progress)
    {
        var originalsContainer = _config["AZURE_STORAGE_CONTAINER_ORIGINALS"] ?? "originals";
        var coversContainer = _config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "cover-thumbnails";

        var existing = (await _bookService.GetBooksByUserIdAsync(userId)).Select(b => b.OriginalFilename).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0;

        var connString = new SqliteConnectionStringBuilder { DataSource = metadataDbPath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(connString);
        conn.Open();

        // Count books to provide progress
        var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM books";
        var total = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
        int processed = 0;

        // Read books
        var bookCmd = conn.CreateCommand();
        bookCmd.CommandText = @"SELECT id, title, path, has_cover, pubdate, series_index, isbn, timestamp, last_modified FROM books ORDER BY id";

        using var reader = bookCmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var bookId = reader.GetInt32(0);
                var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var relPath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var hasCover = !reader.IsDBNull(3) && reader.GetInt32(3) == 1;
                var pubDate = reader.IsDBNull(4) ? null : (string?)reader.GetString(4);
                var seriesIndex = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                var isbn = reader.IsDBNull(6) ? null : (string?)reader.GetString(6);

                var bookFolder = Path.Combine(calibreRootPath, relPath);

                // find formats for this book
                var formatsCmd = conn.CreateCommand();
                formatsCmd.CommandText = @"SELECT format, name FROM data WHERE book = $book";
                formatsCmd.Parameters.AddWithValue("$book", bookId);
                var formats = new List<(string fmt, string name)>();
                using (var fr = formatsCmd.ExecuteReader())
                {
                    while (fr.Read())
                    {
                        formats.Add((fr.GetString(0), fr.GetString(1)));
                    }
                }

                // pick preferred format file path
                string? formatFilePath = null;
                foreach (var pref in PreferredFormats)
                {
                    var match = formats.FirstOrDefault(f => string.Equals(f.fmt, pref, StringComparison.OrdinalIgnoreCase));
                    if (match != default)
                    {
                        var candidate = Path.Combine(bookFolder, $"{match.name}.{match.fmt.ToLowerInvariant()}");
                        if (File.Exists(candidate)) { formatFilePath = candidate; break; }
                    }
                }

                // fallback to any file in folder with acceptable extension
                if (formatFilePath == null && Directory.Exists(bookFolder))
                {
                    var any = Directory.EnumerateFiles(bookFolder).FirstOrDefault(ff => BookExtensions.Contains(Path.GetExtension(ff), StringComparer.OrdinalIgnoreCase));
                    if (any != null) formatFilePath = any;
                }

                if (formatFilePath == null)
                {
                    _logger.LogWarning("No format file found for Calibre book {Id} in folder {Folder}", bookId, bookFolder);
                    skipped++;
                    processed++;
                    progress?.Report((processed, total));
                    continue;
                }

                var originalFilename = Path.GetFileName(formatFilePath);
                if (existing.Contains(originalFilename)) { skipped++; processed++; progress?.Report((processed, total)); continue; }

                // upload original
                await using var fs = File.OpenRead(formatFilePath);
                var filename = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{originalFilename}";
                var fileEntity = await _fileService.GetOrUploadFileAsync(fs, filename, userId, originalsContainer);

                // upload cover if present
                string? coverPath = null;
                int? coverFileId = null;
                if (importCovers)
                {
                    try
                    {
                        if (hasCover)
                        {
                            var coverCandidate = Path.Combine(bookFolder, "cover.jpg");
                            if (!File.Exists(coverCandidate)) coverCandidate = Directory.EnumerateFiles(bookFolder).FirstOrDefault(p => Path.GetFileName(p).ToLowerInvariant().StartsWith("cover."));
                            if (coverCandidate != null && File.Exists(coverCandidate))
                            {
                                await using var cs = File.OpenRead(coverCandidate);
                                // Compute SHA256 and attempt to reuse existing file by hash
                                cs.Position = 0;
                                var sha = ComputeSha256Hex(cs);
                                var existingCover = await _fileService.GetFileByHashAsync(sha);
                                if (existingCover != null)
                                {
                                    coverPath = existingCover.StoragePath;
                                    coverFileId = existingCover.Id;
                                }
                                else
                                {
                                    cs.Position = 0;
                                    var coverName = $"thumb_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(coverCandidate)}";
                                    var coverEntity = await _fileService.GetOrUploadFileAsync(cs, coverName, userId, coversContainer);
                                    coverPath = coverEntity.StoragePath;
                                    coverFileId = coverEntity.Id;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Cover upload failed for Calibre book {Id}", bookId);
                    }
                }

                var info = new FileInfo(formatFilePath);
                var bookEntity = new BookEntity
                {
                    UserId = userId,
                    Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(formatFilePath) : title,
                    Authors = await GetAuthorsForBookAsync(conn, bookId),
                    OriginalFilename = originalFilename,
                    FilePath = fileEntity.StoragePath,
                    FileSize = fileEntity.Size,
                    MimeType = GetMimeType(originalFilename),
                    CoverPath = coverPath,
                    FileId = fileEntity.Id,
                    CoverFileId = coverFileId,
                    CreatedAt = info.CreationTimeUtc,
                    UpdatedAt = info.LastWriteTimeUtc,

                    Publisher = await GetPublisherForBookAsync(conn, bookId),
                    Isbn = isbn,
                    PublishedAt = pubDate,
                    Series = await GetSeriesForBookAsync(conn, bookId),
                    SeriesIndex = seriesIndex.HasValue ? (short?)seriesIndex.Value : null,
                    Tags = await GetTagsForBookAsync(conn, bookId)
                };

                await _bookService.CreateBookAsync(bookEntity);
                existing.Add(originalFilename);
                imported++;
                processed++;
                progress?.Report((processed, total));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import Calibre book record");
                processed++;
                progress?.Report((processed, total));
            }
        }

        return (imported, skipped);
    }

    private static string GetMimeType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".epub" => "application/epub+zip",
            ".mobi" => "application/x-mobipocket-ebook",
            ".azw3" => "application/vnd.amazon.ebook",
            ".pdf" => "application/pdf",
            ".fb2" => "application/fb2",
            _ => "application/octet-stream"
        };
    }

    private static async Task<string?> GetAuthorsForBookAsync(SqliteConnection conn, int bookId)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT a.name FROM authors a JOIN books_authors_link bal ON a.id = bal.author WHERE bal.book = $book ORDER BY bal.id";
        cmd.Parameters.AddWithValue("$book", bookId);
        var names = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) names.Add(r.GetString(0));
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static async Task<string?> GetTagsForBookAsync(SqliteConnection conn, int bookId)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT t.name FROM tags t JOIN books_tags_link btl ON t.id = btl.tag WHERE btl.book = $book";
        cmd.Parameters.AddWithValue("$book", bookId);
        var names = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) names.Add(r.GetString(0));
        return names.Count == 0 ? null : string.Join(",", names);
    }

    private static async Task<string?> GetSeriesForBookAsync(SqliteConnection conn, int bookId)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT s.name FROM series s JOIN books_series_link bsl ON s.id = bsl.series WHERE bsl.book = $book";
        cmd.Parameters.AddWithValue("$book", bookId);
        using var r = cmd.ExecuteReader();
        if (r.Read()) return r.GetString(0);
        return null;
    }

    private static async Task<string?> GetPublisherForBookAsync(SqliteConnection conn, int bookId)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT p.name FROM publishers p JOIN books_publishers_link bpl ON p.id = bpl.publisher WHERE bpl.book = $book";
        cmd.Parameters.AddWithValue("$book", bookId);
        using var r = cmd.ExecuteReader();
        if (r.Read()) return r.GetString(0);
        return null;
    }

    private static string ComputeSha256Hex(Stream stream)
    {
        using (var sha256 = SHA256.Create())
        {
            // Ensure stream is at beginning
            if (stream.CanSeek) stream.Position = 0;
            var hash = sha256.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
