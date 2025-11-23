using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Data;
using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly AppDbContext _db;
    public CollectionRepository(AppDbContext db) => _db = db;

    public async Task<CollectionEntity[]> GetCollectionsByUserIdAsync(int userId)
    {
        return await _db.Collections.Where(c => c.UserId == userId).OrderBy(c => c.Name).ToArrayAsync();
    }

    public async Task<CollectionEntity?> GetCollectionByIdAsync(int id)
    {
        return await _db.Collections.FindAsync(id);
    }

    public async Task<CollectionEntity> AddAsync(CollectionEntity entity)
    {
        _db.Collections.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<CollectionEntity> UpdateAsync(CollectionEntity entity)
    {
        var existing = await _db.Collections.FindAsync(entity.Id);
        if (existing == null)
        {
            _db.Collections.Attach(entity);
            _db.Entry(entity).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return entity;
        }

        _db.Entry(existing).CurrentValues.SetValues(entity);
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id, int userId)
    {
        var c = await _db.Collections.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c == null) return;
        _db.Collections.Remove(c);
        await _db.SaveChangesAsync();
    }

    public async Task<BookCollectionEntity> AddBookAsync(BookCollectionEntity bc)
    {
        _db.BookCollections.Add(bc);
        await _db.SaveChangesAsync();
        return bc;
    }

    public async Task RemoveBookAsync(int bookId, int collectionId)
    {
        var e = await _db.BookCollections.FirstOrDefaultAsync(bc => bc.BookId == bookId && bc.CollectionId == collectionId);
        if (e == null) return;
        _db.BookCollections.Remove(e);
        await _db.SaveChangesAsync();
    }

    public async Task<BookCollectionEntity[]> GetBooksInCollectionAsync(int collectionId)
    {
        return await _db.BookCollections.Where(bc => bc.CollectionId == collectionId).ToArrayAsync();
    }
}
