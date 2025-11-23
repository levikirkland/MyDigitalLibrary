using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Repositories;

namespace MyDigitalLibrary.Core.Services;

public class BookService : IBookService
{
    private readonly IBookRepository _repo;
    private readonly IFileService _fileService;
    public BookService(IBookRepository repo, IFileService fileService) => (_repo, _fileService) = (repo, fileService);

    private static Book Map(BookEntity e) => new Book
    {
        // persisted fields -> model
        Id = e.Id,
        UserId = e.UserId,
        Title = e.Title,
        Authors = e.Authors,
        Description = e.Description,
        OriginalFilename = e.OriginalFilename,
        FilePath = e.FilePath,
        FileSize = e.FileSize,
        MimeType = e.MimeType,
        CoverPath = e.CoverPath,
        FileId = e.FileId,
        CoverFileId = e.CoverFileId,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,

        // Extended metadata
        Publisher = e.Publisher,
        Isbn = e.Isbn,
        PublishedAt = e.PublishedAt,
        Language = e.Language,
        Series = e.Series,
        SeriesIndex = e.SeriesIndex.HasValue ? (int?)e.SeriesIndex.Value : null,
        Rating = e.Rating,
        Tags = e.Tags,

        // Reading progress
        Status = e.Status,
        ProgressPercent = e.ProgressPercent,
        CurrentPage = e.CurrentPage,
        TotalPages = e.TotalPages,
        StartedAt = e.StartedAt,
        FinishedAt = e.FinishedAt
    };

    public async Task<Book[]> GetBooksByUserIdAsync(int userId)
    {
        var entities = await _repo.GetBooksByUserIdAsync(userId);
        return entities.Select(Map).ToArray();
    }

    public async Task<Book[]> SearchBooksByUserIdAsync(int userId, string query)
    {
        var entities = await _repo.SearchBooksByUserIdAsync(userId, query);
        return entities.Select(Map).ToArray();
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        var e = await _repo.GetBookByIdAsync(id);
        return e == null ? null : Map(e);
    }

    public async Task<Book> CreateBookAsync(BookEntity bookEntity)
    {
        var added = await _repo.AddAsync(bookEntity);
        return Map(added);
    }

    public async Task<Book> UpdateBookAsync(BookEntity bookEntity)
    {
        var updated = await _repo.UpdateAsync(bookEntity);
        return Map(updated);
    }

    public async Task DeleteBookAsync(int id, int userId)
    {
        // Fetch existing book info first to know which files to decrement
        var book = await GetBookByIdAsync(id);
        if (book == null) return;
        if (book.UserId != userId) return; // do nothing if caller isn't owner

        // Delete book record
        await _repo.DeleteBookAsync(id, userId);

        // Decrement refcounts for associated files (original and cover) asynchronously
        try
        {
            if (book.FileId.HasValue)
            {
                await _fileService.DecrementRefCountAsync(book.FileId.Value);
            }
            if (book.CoverFileId.HasValue)
            {
                await _fileService.DecrementRefCountAsync(book.CoverFileId.Value);
            }
        }
        catch
        {
            // Swallow exceptions here to avoid failing delete if storage cleanup has issues.
        }
    }
}