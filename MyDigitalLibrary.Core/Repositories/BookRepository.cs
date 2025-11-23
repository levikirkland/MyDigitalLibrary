using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Repositories;

public class BookRepository : IBookRepository
{
    private readonly AppDbContext _db;
    public BookRepository(AppDbContext db) => _db = db;

    public async Task<BookEntity[]> GetBooksByUserIdAsync(int userId)
    {
        return await _db.Books
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToArrayAsync();
    }

    public async Task<BookEntity[]> SearchBooksByUserIdAsync(int userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return await GetBooksByUserIdAsync(userId);

        query = query.Trim().ToLowerInvariant();
        return await _db.Books
            .Where(b => b.UserId == userId &&
                        (EF.Functions.Like(b.Title.ToLower(), $"%{query}%")
                         || (b.Authors != null && EF.Functions.Like(b.Authors.ToLower(), $"%{query}%"))
                         || (b.Description != null && EF.Functions.Like(b.Description.ToLower(), $"%{query}%"))))
            .OrderByDescending(b => b.CreatedAt)
            .ToArrayAsync();
    }

    public async Task<BookEntity?> GetBookByIdAsync(int id)
    {
        return await _db.Books.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<BookEntity> AddAsync(BookEntity book)
    {
        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        return book;
    }

    public async Task<BookEntity> UpdateAsync(BookEntity book)
    {
        // Avoid attaching a second instance with the same key. Fetch existing tracked entity and apply values.
        var existing = await _db.Books.FindAsync(book.Id);
        if (existing == null)
        {
            // If not found in the context/DB, attach the incoming entity
            _db.Books.Attach(book);
            _db.Entry(book).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return book;
        }

        // Copy values from incoming entity to the tracked entity
        _db.Entry(existing).CurrentValues.SetValues(book);
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteBookAsync(int id, int userId)
    {
        var e = await _db.Books.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (e == null) return;
        _db.Books.Remove(e);
        await _db.SaveChangesAsync();
    }
}
