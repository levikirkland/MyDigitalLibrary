using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;
using MyDigitalLibrary.Entities;
using MyDigitalLibrary.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MyDigitalLibrary.Pages.Books;

public class EditModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IFileService _fileService;
    private readonly IConfiguration _config;

    public EditModel(IBookService bookService, IFileService fileService, IConfiguration config)
    {
        _bookService = bookService;
        _fileService = fileService;
        _config = config;
    }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public string? Title { get; set; }

    [BindProperty]
    public string? Authors { get; set; }

    [BindProperty]
    public IFormFile? Cover { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    // Additional metadata
    [BindProperty]
    public string? Publisher { get; set; }
    [BindProperty]
    public string? Isbn { get; set; }
    [BindProperty]
    public string? PublishedAt { get; set; }
    [BindProperty]
    public string? Language { get; set; }
    [BindProperty]
    public string? Series { get; set; }
    [BindProperty]
    public int? SeriesIndex { get; set; }
    [BindProperty]
    public byte? Rating { get; set; }
    [BindProperty]
    public string? Tags { get; set; }

    // Reading progress
    [BindProperty]
    public string? Status { get; set; }
    [BindProperty]
    public byte? ProgressPercent { get; set; }
    [BindProperty]
    public int? CurrentPage { get; set; }
    [BindProperty]
    public int? TotalPages { get; set; }
    [BindProperty]
    public string? StartedAt { get; set; }
    [BindProperty]
    public string? FinishedAt { get; set; }

    public string? ExistingCoverPath { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var book = await _bookService.GetBookByIdAsync(id);
        if (book == null) return NotFound();
        Id = book.Id;
        Title = book.Title;
        Authors = book.Authors;
        Description = book.Description;
        ExistingCoverPath = book.CoverPath;

        // populate extended metadata
        Publisher = book.Publisher;
        Isbn = book.Isbn;
        PublishedAt = book.PublishedAt;
        Language = book.Language;
        Series = book.Series;
        SeriesIndex = book.SeriesIndex;
        Rating = book.Rating;
        Tags = book.Tags;

        Status = book.Status;
        ProgressPercent = book.ProgressPercent;
        CurrentPage = book.CurrentPage;
        TotalPages = book.TotalPages;
        StartedAt = book.StartedAt;
        FinishedAt = book.FinishedAt;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var bookModel = await _bookService.GetBookByIdAsync(Id);
        if (bookModel == null) return NotFound();
        if (bookModel.UserId != userId) return Forbid();

        // Prepare updated entity from existing model
        var bookEntity = new BookEntity
        {
            Id = bookModel.Id,
            UserId = bookModel.UserId,
            Title = string.IsNullOrWhiteSpace(Title) ? bookModel.Title : Title,
            Authors = string.IsNullOrWhiteSpace(Authors) ? bookModel.Authors : Authors,
            Description = string.IsNullOrWhiteSpace(Description) ? bookModel.Description : Description,
            OriginalFilename = bookModel.OriginalFilename,
            FilePath = bookModel.FilePath,
            FileSize = bookModel.FileSize,
            MimeType = bookModel.MimeType,
            CoverPath = bookModel.CoverPath,
            FileId = bookModel.FileId,
            CoverFileId = bookModel.CoverFileId,
            CreatedAt = bookModel.CreatedAt,
            UpdatedAt = DateTime.UtcNow,

            Publisher = string.IsNullOrWhiteSpace(Publisher) ? bookModel.Publisher : Publisher,
            Isbn = string.IsNullOrWhiteSpace(Isbn) ? bookModel.Isbn : Isbn,
            PublishedAt = string.IsNullOrWhiteSpace(PublishedAt) ? bookModel.PublishedAt : PublishedAt,
            Language = string.IsNullOrWhiteSpace(Language) ? bookModel.Language : Language,
            Series = string.IsNullOrWhiteSpace(Series) ? bookModel.Series : Series,
            // convert int? -> short?
            SeriesIndex = SeriesIndex.HasValue ? (short?)SeriesIndex.Value : (bookModel.SeriesIndex.HasValue ? (short?)bookModel.SeriesIndex.Value : null),
            Rating = Rating ?? bookModel.Rating,
            Tags = string.IsNullOrWhiteSpace(Tags) ? bookModel.Tags : Tags,

            Status = string.IsNullOrWhiteSpace(Status) ? bookModel.Status : Status,
            ProgressPercent = ProgressPercent ?? bookModel.ProgressPercent,
            CurrentPage = CurrentPage ?? bookModel.CurrentPage,
            TotalPages = TotalPages ?? bookModel.TotalPages,
            StartedAt = string.IsNullOrWhiteSpace(StartedAt) ? bookModel.StartedAt : StartedAt,
            FinishedAt = string.IsNullOrWhiteSpace(FinishedAt) ? bookModel.FinishedAt : FinishedAt
        };

        int? newCoverFileId = null;
        string? newCoverPath = null;
        if (Cover != null && Cover.Length > 0)
        {
            try
            {
                using var image = await Image.LoadAsync(Cover.OpenReadStream());
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(300, 450), Mode = ResizeMode.Max }));
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms);
                ms.Position = 0;
                var coversContainer = _config["AZURE_STORAGE_CONTAINER_COVERS"] ?? "cover-thumbnails";
                var coverName = $"thumb_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Path.GetFileName(Cover.FileName)}";
                var coverEntity = await _fileService.GetOrUploadFileAsync(ms, coverName, userId, coversContainer);
                newCoverFileId = coverEntity.Id;
                newCoverPath = coverEntity.StoragePath;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to process cover: " + ex.Message;
                return Page();
            }
        }

        // Update entity with new cover if provided
        if (newCoverFileId.HasValue)
        {
            var oldCoverFileId = bookEntity.CoverFileId;
            bookEntity.CoverFileId = newCoverFileId;
            bookEntity.CoverPath = newCoverPath;

            await _bookService.UpdateBookAsync(bookEntity);

            if (oldCoverFileId.HasValue)
            {
                try { await _fileService.DecrementRefCountAsync(oldCoverFileId.Value); } catch { }
            }
        }
        else
        {
            await _bookService.UpdateBookAsync(bookEntity);
        }

        Success = true;
        ExistingCoverPath = bookEntity.CoverPath;
        return Page();
    }
}
