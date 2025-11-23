using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Core.Entities;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    public CollectionService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<CollectionEntity>> GetCollectionsByUserAsync(int userId)
    {
        return await _db.Collections.Where(c => c.UserId == userId).ToListAsync();
    }

    public async Task<CollectionEntity?> GetCollectionAsync(int collectionId)
    {
        return await _db.Collections.FindAsync(collectionId);
    }

    public async Task<CollectionEntity> CreateCollectionAsync(CollectionEntity collection)
    {
        _db.Collections.Add(collection);
        await _db.SaveChangesAsync();
        return collection;
    }

    public async Task UpdateCollectionAsync(CollectionEntity collection)
    {
        _db.Collections.Update(collection);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteCollectionAsync(int collectionId)
    {
        var c = await _db.Collections.FindAsync(collectionId);
        if (c != null)
        {
            _db.Collections.Remove(c);
            await _db.SaveChangesAsync();
        }
    }

    public async Task AddBookToCollectionAsync(int collectionId, int bookId)
    {
        _db.BookCollections.Add(new BookCollectionEntity { BookId = bookId, CollectionId = collectionId });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveBookFromCollectionAsync(int collectionId, int bookId)
    {
        var bc = await _db.BookCollections.FirstOrDefaultAsync(b => b.BookId == bookId && b.CollectionId == collectionId);
        if (bc != null) { _db.BookCollections.Remove(bc); await _db.SaveChangesAsync(); }
    }
}
