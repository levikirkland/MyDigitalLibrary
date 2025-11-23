using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Repositories;

public interface IBookRepository
{
    Task<BookEntity[]> GetBooksByUserIdAsync(int userId);
    Task<BookEntity[]> SearchBooksByUserIdAsync(int userId, string query);
    Task<BookEntity?> GetBookByIdAsync(int id);
    Task DeleteBookAsync(int id, int userId);
    Task<BookEntity> AddAsync(BookEntity book);
    Task<BookEntity> UpdateAsync(BookEntity book);

    // New: filter by dynamic rules
    Task<BookEntity[]> GetByRulesAsync(IEnumerable<Rule> rules, int userId);

    // New: fetch books by their ids
    Task<BookEntity[]> GetBooksByIdsAsync(int[] ids);
}