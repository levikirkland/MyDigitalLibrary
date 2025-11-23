using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public interface IReviewService
{
    Task<ReviewEntity?> GetUserReviewAsync(int bookId, int userId);
    Task<ReviewEntity> UpsertReviewAsync(int bookId, int userId, int rating, string? reviewText);
    Task<IEnumerable<ReviewEntity>> GetBookReviewsAsync(int bookId);
    Task DeleteReviewAsync(int bookId, int userId);
    Task<(double AverageRating, int Count)> GetAverageRatingAsync(int bookId);
}
