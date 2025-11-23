using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Pages.Books;

public class ReadModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IReadingService _readingService;

    public ReadModel(IBookService bookService, IReadingService readingService)
    {
        _bookService = bookService;
        _readingService = readingService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Book? Book { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        Book = await _bookService.GetBookByIdAsync(id);
        if (Book == null) return NotFound();
        if (Book.UserId != userId) return Forbid();

        // Only allow EPUB reading for now
        var ext = System.IO.Path.GetExtension(Book.OriginalFilename ?? "").ToLowerInvariant();
        if (ext != ".epub")
        {
            // Redirect to Download if not EPUB
            return RedirectToPage("/Books/Download", new { id = id });
        }

        // Ensure a reading progress record exists and mark as 'reading' if not already
        var existing = await _readingService.GetReadingProgressAsync(id, userId);
        if (existing == null)
        {
            existing = await _readingService.InitializeReadingProgressAsync(id, userId);
        }

        // Expose last known CFI on the Book model so the reader can restore position
        if (!string.IsNullOrEmpty(existing.LastLocation))
        {
            Book.StartedAt = existing.LastLocation;
        }

        if (existing.Status != "reading")
        {
            var updates = new ReadingProgressEntity
            {
                BookId = id,
                UserId = userId,
                Status = "reading",
                StartedAt = existing.StartedAt ?? DateTime.UtcNow,
                ProgressPercent = existing.ProgressPercent ?? 0
            };
            var rp = await _readingService.UpdateReadingProgressAsync(id, userId, updates);

            // Update book record so library view reflects reading status immediately
            var bookModel = await _bookService.GetBookByIdAsync(id);
            if (bookModel != null && bookModel.UserId == userId)
            {
                var entity = new BookEntity
                {
                    Id = bookModel.Id,
                    UserId = bookModel.UserId,
                    Title = bookModel.Title,
                    Authors = bookModel.Authors,
                    Description = bookModel.Description,
                    OriginalFilename = bookModel.OriginalFilename,
                    FilePath = bookModel.FilePath,
                    FileSize = bookModel.FileSize,
                    MimeType = bookModel.MimeType,
                    CoverPath = bookModel.CoverPath,
                    FileId = bookModel.FileId,
                    CoverFileId = bookModel.CoverFileId,
                    CreatedAt = bookModel.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    Publisher = bookModel.Publisher,
                    Isbn = bookModel.Isbn,
                    PublishedAt = bookModel.PublishedAt,
                    Language = bookModel.Language,
                    Series = bookModel.Series,
                    SeriesIndex = bookModel.SeriesIndex.HasValue ? (short?)bookModel.SeriesIndex.Value : null,
                    Rating = bookModel.Rating,
                    Tags = bookModel.Tags,
                    Status = rp.Status,
                    ProgressPercent = (byte?)(rp.ProgressPercent.HasValue ? (byte?)rp.ProgressPercent.Value : bookModel.ProgressPercent),
                    CurrentPage = rp.CurrentPage,
                    TotalPages = rp.TotalPages,
                    StartedAt = rp.StartedAt?.ToString(),
                    FinishedAt = rp.FinishedAt?.ToString()
                };

                try { await _bookService.UpdateBookAsync(entity); } catch { }

                // refresh Book model to include updated fields (so page markup uses newest values)
                Book = await _bookService.GetBookByIdAsync(id);
            }
        }

        return Page();
    }

    public class ProgressDto
    {
        public string? Status { get; set; }
        public int? ProgressPercent { get; set; }
        public int? CurrentPage { get; set; }
        public int? TotalPages { get; set; }
        public string? EpubLocation { get; set; }
    }

    // POST handler for progress updates
    public async Task<IActionResult> OnPostProgressAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

        var dto = await Request.ReadFromJsonAsync<ProgressDto>();
        if (dto == null) return BadRequest();

        var updates = new ReadingProgressEntity
        {
            BookId = Id,
            UserId = userId,
            Status = dto.Status,
            ProgressPercent = dto.ProgressPercent,
            CurrentPage = dto.CurrentPage,
            TotalPages = dto.TotalPages,
            StartedAt = dto.Status == "reading" ? DateTime.UtcNow : (DateTime?)null,
            FinishedAt = dto.Status == "read" ? DateTime.UtcNow : (DateTime?)null,
            LastLocation = dto.EpubLocation
        };

        var rp = await _readingService.UpdateReadingProgressAsync(Id, userId, updates);

        // Also update book record so UI reflects status/progress
        var bookModel = await _bookService.GetBookByIdAsync(Id);
        if (bookModel != null && bookModel.UserId == userId)
        {
            var entity = new BookEntity
            {
                Id = bookModel.Id,
                UserId = bookModel.UserId,
                Title = bookModel.Title,
                Authors = bookModel.Authors,
                Description = bookModel.Description,
                OriginalFilename = bookModel.OriginalFilename,
                FilePath = bookModel.FilePath,
                FileSize = bookModel.FileSize,
                MimeType = bookModel.MimeType,
                CoverPath = bookModel.CoverPath,
                FileId = bookModel.FileId,
                CoverFileId = bookModel.CoverFileId,
                CreatedAt = bookModel.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                Publisher = bookModel.Publisher,
                Isbn = bookModel.Isbn,
                PublishedAt = bookModel.PublishedAt,
                Language = bookModel.Language,
                Series = bookModel.Series,
                SeriesIndex = bookModel.SeriesIndex.HasValue ? (short?)bookModel.SeriesIndex.Value : null,
                Rating = bookModel.Rating,
                Tags = bookModel.Tags,
                Status = rp.Status,
                ProgressPercent = (byte?)(rp.ProgressPercent.HasValue ? (byte?)rp.ProgressPercent.Value : bookModel.ProgressPercent),
                CurrentPage = rp.CurrentPage,
                TotalPages = rp.TotalPages,
                StartedAt = rp.StartedAt?.ToString(),
                FinishedAt = rp.FinishedAt?.ToString()
            };

            try { await _bookService.UpdateBookAsync(entity); } catch { }
        }

        return new JsonResult(new { success = true, progress = rp.ProgressPercent, status = rp.Status, currentPage = rp.CurrentPage, totalPages = rp.TotalPages });
    }
}
