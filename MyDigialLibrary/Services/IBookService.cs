using MyDigitalLibrary.Entities;
using MyDigitalLibrary.Models;

namespace MyDigitalLibrary.Services;

public interface IBookService
{
    Task<Book[]> GetBooksByUserIdAsync(int userId);
    Task<Book[]> SearchBooksByUserIdAsync(int userId, string query);
    Task<Book?> GetBookByIdAsync(int id);
    Task<Book> CreateBookAsync(BookEntity bookEntity); // added: create and return model
    Task<Book> UpdateBookAsync(BookEntity bookEntity);
    Task DeleteBookAsync(int id, int userId);
}
