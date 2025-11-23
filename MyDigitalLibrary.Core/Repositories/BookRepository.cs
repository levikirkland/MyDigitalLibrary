using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Specifications;

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

    public async Task<BookEntity[]> GetBooksByIdsAsync(int[] ids)
    {
        if (ids == null || ids.Length == 0) return Array.Empty<BookEntity>();
        var entities = await _db.Books.Where(b => ids.Contains(b.Id)).ToListAsync();
        var map = entities.ToDictionary(e => e.Id);
        var ordered = ids.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToArray();
        return ordered;
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

    public async Task<BookEntity[]> GetByRulesAsync(IEnumerable<Rule> rules, int userId)
    {
        if (rules == null || !rules.Any()) return await GetBooksByUserIdAsync(userId);

        // Build combined predicate: AND all rule specifications
        Expression<Func<BookEntity, bool>>? combined = null;
        foreach (var r in rules)
        {
            var spec = new RuleSpecification(r);
            if (combined == null) combined = spec.Criteria;
            else combined = CombineExpressions(combined, spec.Criteria);
        }

        IQueryable<BookEntity> q = _db.Books.Where(b => b.UserId == userId);
        if (combined != null)
        {
            q = q.Where(combined);
        }

        return await q.OrderByDescending(b => b.CreatedAt).ToArrayAsync();
    }

    private static Expression<Func<T, bool>> CombineExpressions<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T));
        var leftBody = Expression.Invoke(left, param);
        var rightBody = Expression.Invoke(right, param);
        var combined = Expression.AndAlso(leftBody, rightBody);
        return Expression.Lambda<Func<T, bool>>(combined, param);
    }
}
