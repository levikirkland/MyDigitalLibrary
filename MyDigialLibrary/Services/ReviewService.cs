using MyDigitalLibrary.Data;
using MyDigitalLibrary.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    public ReviewService(AppDbContext db) => _db = db;

    public async Task<ReviewEntity?> GetUserReviewAsync(int bookId, int userId)
    {
        return await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
    }

    public async Task<ReviewEntity> UpsertReviewAsync(int bookId, int userId, int rating, string? reviewText)
    {
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
        if (existing != null)
        {
            existing.Rating = rating;
            existing.ReviewText = reviewText;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new ReviewEntity { BookId = bookId, UserId = userId, Rating = rating, ReviewText = reviewText };
            _db.Reviews.Add(existing);
        }
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<IEnumerable<ReviewEntity>> GetBookReviewsAsync(int bookId)
    {
        return await _db.Reviews.Where(r => r.BookId == bookId).ToListAsync();
    }

    public async Task DeleteReviewAsync(int bookId, int userId)
    {
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
        if (existing != null)
        {
            _db.Reviews.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<(double AverageRating, int Count)> GetAverageRatingAsync(int bookId)
    {
        var ratings = await _db.Reviews.Where(r => r.BookId == bookId).ToListAsync();
        if (!ratings.Any()) return (0.0, 0);
        return (ratings.Average(r => r.Rating), ratings.Count);
    }
}
