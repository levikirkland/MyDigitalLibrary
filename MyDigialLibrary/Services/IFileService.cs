using MyDigitalLibrary.Entities;
namespace MyDigitalLibrary.Services;

public interface IFileService
{
    Task<FileEntity> GetOrUploadFileAsync(Stream inputStream, string filename, int userId, string? containerName = null);
    Task<FileEntity?> GetFileByHashAsync(string sha256);
    Task<FileEntity?> GetFileByIdAsync(int id);
    Task DecrementRefCountAsync(int fileId);
}
