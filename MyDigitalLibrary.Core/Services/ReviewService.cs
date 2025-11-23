using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    public ReviewService(AppDbContext db) => _db = db;

    public async Task<ReviewEntity?> GetUserReviewAsync(int bookId, int userId)
    {
        return await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
    }

    public async Task<ReviewEntity> UpsertReviewAsync(int bookId, int userId, int rating, string? reviewText, bool isPublic = true)
    {
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
        if (existing != null)
        {
            existing.Rating = rating;
            existing.ReviewText = reviewText;
            var becamePublic = !existing.IsPublic && isPublic;
            existing.IsPublic = isPublic;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (becamePublic)
            {
                // publish metadata
                var book = await _db.Books.FindAsync(bookId);
                if (book != null) await CreatePublicMetadataAsync(existing, book);
            }
        }
        else
        {
            existing = new ReviewEntity { BookId = bookId, UserId = userId, Rating = rating, ReviewText = reviewText, IsPublic = isPublic };
            _db.Reviews.Add(existing);
            await _db.SaveChangesAsync();

            if (isPublic)
            {
                var book = await _db.Books.FindAsync(bookId);
                if (book != null) await CreatePublicMetadataAsync(existing, book);
            }
        }

        return existing;
    }

    public async Task<IEnumerable<ReviewEntity>> GetBookReviewsAsync(int bookId, bool includePrivate = false)
    {
        if (includePrivate)
            return await _db.Reviews.Where(r => r.BookId == bookId).ToListAsync();
        return await _db.Reviews.Where(r => r.BookId == bookId && r.IsPublic).ToListAsync();
    }

    public async Task<IEnumerable<ReviewDisplay>> GetBookReviewsForDisplayAsync(int bookId, int? currentUserId = null)
    {
        // join reviews with users to get display name, and respect user's ShareReviews and review.IsPublic
        var query = from r in _db.Reviews
                    join u in _db.Users on r.UserId equals u.Id
                    where r.BookId == bookId && (r.IsPublic || (currentUserId.HasValue && r.UserId == currentUserId.Value))
                    select new ReviewDisplay
                    {
                        Id = r.Id,
                        BookId = r.BookId,
                        UserId = r.UserId,
                        DisplayName = string.IsNullOrEmpty(u.DisplayName) ? u.Email : u.DisplayName,
                        Rating = r.Rating,
                        ReviewText = r.ReviewText,
                        IsPublic = r.IsPublic,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt
                    };

        return await query.OrderByDescending(r => r.UpdatedAt).ToListAsync();
    }

    public async Task DeleteReviewAsync(int bookId, int userId)
    {
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
        if (existing != null)
        {
            // if it was public, remove public metadata
            if (existing.IsPublic)
            {
                await RemovePublicMetadataByReviewIdAsync(existing.Id);
            }

            _db.Reviews.Remove(existing);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<(double AverageRating, int Count)> GetAverageRatingAsync(int bookId, bool includePrivate = false)
    {
        var query = _db.Reviews.Where(r => r.BookId == bookId);
        if (!includePrivate) query = query.Where(r => r.IsPublic);
        var ratings = await query.ToListAsync();
        if (!ratings.Any()) return (0.0, 0);
        return (ratings.Average(r => r.Rating), ratings.Count);
    }

    public async Task<ReviewEntity?> ToggleReviewPublicAsync(int reviewId, int userId)
    {
        var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);
        if (existing == null) return null;
        existing.IsPublic = !existing.IsPublic;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (existing.IsPublic)
        {
            var book = await _db.Books.FindAsync(existing.BookId);
            if (book != null) await CreatePublicMetadataAsync(existing, book);
        }
        else
        {
            await RemovePublicMetadataByReviewIdAsync(existing.Id);
        }

        return existing;
    }

    public async Task CreatePublicMetadataAsync(ReviewEntity review, BookEntity book)
    {
        // remove existing for this review if present
        await RemovePublicMetadataByReviewIdAsync(review.Id);

        var pub = new PublicBookEntity
        {
            BookId = book.Id,
            ReviewId = review.Id,
            Title = book.Title ?? string.Empty,
            Authors = book.Authors,
            CoverPath = book.CoverPath,
            Publisher = book.Publisher,
            PublishedAt = book.PublishedAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Add(pub);
        await _db.SaveChangesAsync();
    }

    public async Task RemovePublicMetadataByReviewIdAsync(int reviewId)
    {
        var exists = await _db.Set<PublicBookEntity>().FirstOrDefaultAsync(p => p.ReviewId == reviewId);
        if (exists != null)
        {
            _db.Remove(exists);
            await _db.SaveChangesAsync();
        }
    }
}
