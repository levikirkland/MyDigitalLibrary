using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public interface IReadingService
{
    Task<ReadingProgressEntity?> GetReadingProgressAsync(int bookId, int userId);
    Task<ReadingProgressEntity> UpdateReadingProgressAsync(int bookId, int userId, ReadingProgressEntity updates);
    Task<ReadingProgressEntity> InitializeReadingProgressAsync(int bookId, int userId);
}
