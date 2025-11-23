using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public interface ICollectionService
{
    Task<IEnumerable<CollectionEntity>> GetCollectionsByUserAsync(int userId);
    Task<CollectionEntity?> GetCollectionAsync(int collectionId);
    Task<CollectionEntity> CreateCollectionAsync(CollectionEntity collection);
    Task UpdateCollectionAsync(CollectionEntity collection);
    Task DeleteCollectionAsync(int collectionId);

    Task AddBookToCollectionAsync(int collectionId, int bookId);
    Task RemoveBookFromCollectionAsync(int collectionId, int bookId);

    Task<int> GetBookCountAsync(int collectionId);

    // Rule operations
    Task<CollectionRuleEntity[]> GetRulesForCollectionAsync(int collectionId);
    Task<CollectionRuleEntity> AddRuleAsync(CollectionRuleEntity rule);
    Task RemoveRuleAsync(int ruleId);

    // Members
    Task<BookCollectionEntity[]> GetBooksInCollectionAsync(int collectionId);
}
