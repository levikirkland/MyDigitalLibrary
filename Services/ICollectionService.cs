using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Services;

public interface ICollectionService
{
    Task<IEnumerable<CollectionEntity>> GetCollectionsByUserAsync(int userId);
    Task<CollectionEntity?> GetCollectionAsync(int collectionId);
    Task<CollectionEntity> CreateCollectionAsync(CollectionEntity collection);
    Task UpdateCollectionAsync(CollectionEntity collection);
    Task DeleteCollectionAsync(int collectionId);
    Task AddBookToCollectionAsync(int collectionId, int bookId);
    Task RemoveBookFromCollectionAsync(int collectionId, int bookId);
}
