using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public class CollectionService : ICollectionService
{
    private readonly ICollectionRepository _repo;
    public CollectionService(ICollectionRepository repo) => _repo = repo;

    public async Task<IEnumerable<CollectionEntity>> GetCollectionsByUserAsync(int userId) => await _repo.GetCollectionsByUserIdAsync(userId);
    public async Task<CollectionEntity?> GetCollectionAsync(int collectionId) => await _repo.GetCollectionByIdAsync(collectionId);
    public async Task<CollectionEntity> CreateCollectionAsync(CollectionEntity collection) => await _repo.AddAsync(collection);
    public async Task UpdateCollectionAsync(CollectionEntity collection) => await _repo.UpdateAsync(collection);
    public async Task DeleteCollectionAsync(int collectionId)
    {
        var c = await _repo.GetCollectionByIdAsync(collectionId);
        if (c != null) await _repo.DeleteAsync(collectionId, c.UserId);
    }

    public async Task AddBookToCollectionAsync(int collectionId, int bookId)
    {
        // prevent duplicate entries
        var members = await _repo.GetBooksInCollectionAsync(collectionId);
        if (members.Any(m => m.BookId == bookId))
        {
            throw new InvalidOperationException("book-already-in-collection");
        }

        await _repo.AddBookAsync(new BookCollectionEntity { CollectionId = collectionId, BookId = bookId });
    }
    public async Task RemoveBookFromCollectionAsync(int collectionId, int bookId) => await _repo.RemoveBookAsync(bookId, collectionId);
    public async Task<int> GetBookCountAsync(int collectionId) => await _repo.GetBookCountAsync(collectionId);

    public async Task<CollectionRuleEntity[]> GetRulesForCollectionAsync(int collectionId) => await _repo.GetRulesForCollectionAsync(collectionId);
    public async Task<CollectionRuleEntity> AddRuleAsync(CollectionRuleEntity rule) => await _repo.AddRuleAsync(rule);
    public async Task RemoveRuleAsync(int ruleId) => await _repo.RemoveRuleAsync(ruleId);

    public async Task<BookCollectionEntity[]> GetBooksInCollectionAsync(int collectionId) => await _repo.GetBooksInCollectionAsync(collectionId);
}
