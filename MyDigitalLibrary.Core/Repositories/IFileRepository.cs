using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Repositories
{
    public interface IFileRepository
    {
        Task<FileEntity> AddAsync(FileEntity file);
        Task DeleteAsync(FileEntity file);
        Task<FileEntity?> GetByIdAsync(int id);
        Task<FileEntity?> GetByShaAsync(string sha);
        Task SaveChangesAsync();
    }
}