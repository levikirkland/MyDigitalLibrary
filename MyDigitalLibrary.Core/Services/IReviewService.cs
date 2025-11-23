using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Services;

public interface IReviewService
{
    Task<ReviewEntity?> GetUserReviewAsync(int bookId, int userId);
    Task<ReviewEntity> UpsertReviewAsync(int bookId, int userId, int rating, string? reviewText, bool isPublic = true);
    Task<IEnumerable<ReviewEntity>> GetBookReviewsAsync(int bookId, bool includePrivate = false);
    Task<IEnumerable<ReviewDisplay>> GetBookReviewsForDisplayAsync(int bookId, int? currentUserId = null);
    Task DeleteReviewAsync(int bookId, int userId);
    Task<(double AverageRating, int Count)> GetAverageRatingAsync(int bookId, bool includePrivate = false);
    Task<ReviewEntity?> ToggleReviewPublicAsync(int reviewId, int userId);

    // Public metadata lifecycle
    Task CreatePublicMetadataAsync(ReviewEntity review, BookEntity book);
    Task RemovePublicMetadataByReviewIdAsync(int reviewId);
}
