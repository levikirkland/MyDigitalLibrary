using MyDigitalLibrary.Data;
using MyDigitalLibrary.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Services;

public class ReadingService : IReadingService
{
    private readonly AppDbContext _db;
    public ReadingService(AppDbContext db) => _db = db;

    public async Task<ReadingProgressEntity?> GetReadingProgressAsync(int bookId, int userId)
    {
        return await _db.ReadingProgress.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);
    }

    public async Task<ReadingProgressEntity> InitializeReadingProgressAsync(int bookId, int userId)
    {
        var existing = await GetReadingProgressAsync(bookId, userId);
        if (existing != null) return existing;
        var r = new ReadingProgressEntity { BookId = bookId, UserId = userId, Status = "unread", ProgressPercent = 0 };
        _db.ReadingProgress.Add(r);
        await _db.SaveChangesAsync();
        return r;
    }

    public async Task<ReadingProgressEntity> UpdateReadingProgressAsync(int bookId, int userId, ReadingProgressEntity updates)
    {
        var existing = await GetReadingProgressAsync(bookId, userId);
        if (existing == null) existing = await InitializeReadingProgressAsync(bookId, userId);
        existing.Status = updates.Status ?? existing.Status;
        existing.ProgressPercent = updates.ProgressPercent ?? existing.ProgressPercent;
        existing.CurrentPage = updates.CurrentPage ?? existing.CurrentPage;
        existing.TotalPages = updates.TotalPages ?? existing.TotalPages;
        existing.LastLocation = updates.LastLocation ?? existing.LastLocation;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return existing;
    }
}
