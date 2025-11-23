namespace MyDigitalLibrary.Services;

public interface IStorageService
{
    Task<string> SaveFileAsync(Stream inputStream, string filename, int userId, string? containerName = null);
    Task DeleteFileAsync(string path);
    Task<string> GetDownloadUrlAsync(string storagePath, TimeSpan? expires = null);
    Task<Stream> OpenReadAsync(string storagePath);
}
