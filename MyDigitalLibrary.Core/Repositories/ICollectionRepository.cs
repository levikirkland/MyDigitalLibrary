using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Core.Entities;

namespace MyDigitalLibrary.Core.Repositories;

public interface ICollectionRepository
{
    Task<CollectionEntity[]> GetCollectionsByUserIdAsync(int userId);
    Task<CollectionEntity?> GetCollectionByIdAsync(int id);
    Task<CollectionEntity> AddAsync(CollectionEntity entity);
    Task<CollectionEntity> UpdateAsync(CollectionEntity entity);
    Task DeleteAsync(int id, int userId);

    Task<BookCollectionEntity> AddBookAsync(BookCollectionEntity bc);
    Task RemoveBookAsync(int bookId, int collectionId);
    Task<BookCollectionEntity[]> GetBooksInCollectionAsync(int collectionId);

    Task<int> GetBookCountAsync(int collectionId);

    // Rule operations
    Task<CollectionRuleEntity> AddRuleAsync(CollectionRuleEntity rule);
    Task RemoveRuleAsync(int id);
    Task<CollectionRuleEntity[]> GetRulesForCollectionAsync(int collectionId);
}
