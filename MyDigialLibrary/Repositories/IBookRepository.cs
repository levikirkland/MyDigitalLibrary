using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Repositories;

public interface IBookRepository
{
    Task<BookEntity[]> GetBooksByUserIdAsync(int userId);
    Task<BookEntity[]> SearchBooksByUserIdAsync(int userId, string query);
    Task<BookEntity?> GetBookByIdAsync(int id);
    Task DeleteBookAsync(int id, int userId);
    Task<BookEntity> AddAsync(BookEntity book);
    Task<BookEntity> UpdateAsync(BookEntity book);
}