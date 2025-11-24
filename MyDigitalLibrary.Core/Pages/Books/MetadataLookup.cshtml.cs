using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Entities;
using System.Text.Json.Serialization;
using System.Security.Claims;

namespace MyDigitalLibrary.Core.Pages.Books;

[ValidateAntiForgeryToken]
public class MetadataLookupModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IFeatureService _featureService;
    private readonly IGoogleBooksService _googleService;

    public MetadataLookupModel(IBookService bookService, IFeatureService featureService, IGoogleBooksService googleService)
    {
        _bookService = bookService;
        _featureService = featureService;
        _googleService = googleService;
    }

    [BindProperty(SupportsGet = true)]
    public int BookId { get; set; }

    public bool Allowed { get; set; }
    public GoogleBook[] Results { get; set; } = Array.Empty<GoogleBook>();

    // Current book snapshot for client-side diff
    public MyDigitalLibrary.Core.Models.Book? CurrentBook { get; set; }

    private string? GetUserIdClaim()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!string.IsNullOrWhiteSpace(idClaim)) return idClaim;
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var idClaim = GetUserIdClaim();
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        // check feature
        var feats = await _featureService.GetFeaturesForUserAsync(userId);
        Allowed = feats.Any(f => string.Equals(f.Name, "google_import", StringComparison.OrdinalIgnoreCase) && f.Enabled);
        if (!Allowed) return Forbid();

        var bookModel = await _bookService.GetBookByIdAsync(BookId);
        if (bookModel == null) return NotFound();
        if (bookModel.UserId != userId) return Forbid();

        CurrentBook = bookModel; // model is MyDigitalLibrary.Core.Models.Book from service

        return Page();
    }

    public async Task<IActionResult> OnGetSearchAsync(string? title, string? author)
    {
        var idClaim = GetUserIdClaim();
        if (!int.TryParse(idClaim, out var userId)) return new JsonResult(new { error = "unauthorized" }) { StatusCode = 401 };

        var feats = await _featureService.GetFeaturesForUserAsync(userId);
        if (!feats.Any(f => string.Equals(f.Name, "google_import", StringComparison.OrdinalIgnoreCase) && f.Enabled))
        {
            return new JsonResult(new { error = "feature_disabled" }) { StatusCode = 403 };
        }

        var results = await _googleService.SearchByTitleAsync(title, author, maxResults: 10);
        return new JsonResult(results);
    }

    public class ApplySelectedDto
    {
        public GoogleBook? Google { get; set; }
        public string[]? SelectedFields { get; set; }
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostApplySelectedAsync()
    {
        var idClaim = GetUserIdClaim();
        if (!int.TryParse(idClaim, out var userId)) return new JsonResult(new { error = "unauthorized" }) { StatusCode = 401 };

        var payload = await Request.ReadFromJsonAsync<ApplySelectedDto>();
        if (payload == null || payload.Google == null || payload.SelectedFields == null) return new JsonResult(new { error = "bad_request" }) { StatusCode = 400 };

        var bookModel = await _bookService.GetBookByIdAsync(BookId);
        if (bookModel == null) return new JsonResult(new { error = "notfound" }) { StatusCode = 404 };
        if (bookModel.UserId != userId) return new JsonResult(new { error = "forbidden" }) { StatusCode = 403 };

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

            Status = bookModel.Status,
            ProgressPercent = bookModel.ProgressPercent,
            CurrentPage = bookModel.CurrentPage,
            TotalPages = bookModel.TotalPages,
            StartedAt = bookModel.StartedAt,
            FinishedAt = bookModel.FinishedAt
        };

        // Apply only selected fields
        var vi = payload.Google.VolumeInfo;
        foreach (var field in payload.SelectedFields)
        {
            switch (field)
            {
                case "title":
                    if (!string.IsNullOrWhiteSpace(vi.Title)) entity.Title = vi.Title!;
                    break;
                case "authors":
                    if (vi.Authors != null) entity.Authors = string.Join(", ", vi.Authors);
                    break;
                case "description":
                    if (!string.IsNullOrWhiteSpace(vi.Description)) entity.Description = vi.Description;
                    break;
                case "publisher":
                    if (!string.IsNullOrWhiteSpace(vi.Publisher)) entity.Publisher = vi.Publisher;
                    break;
                case "publishedAt":
                    if (!string.IsNullOrWhiteSpace(vi.PublishedDate)) entity.PublishedAt = vi.PublishedDate;
                    break;
                case "pageCount":
                    if (vi.PageCount.HasValue) entity.TotalPages = vi.PageCount;
                    break;
                case "language":
                    if (!string.IsNullOrWhiteSpace(vi.Language)) entity.Language = vi.Language;
                    break;
                case "isbn":
                    if (vi.IndustryIdentifiers != null)
                    {
                        var isbn13 = vi.IndustryIdentifiers.FirstOrDefault(i => i.Type?.Equals("ISBN_13", StringComparison.OrdinalIgnoreCase) == true)?.Identifier;
                        var isbn10 = vi.IndustryIdentifiers.FirstOrDefault(i => i.Type?.Equals("ISBN_10", StringComparison.OrdinalIgnoreCase) == true)?.Identifier;
                        entity.Isbn = isbn13 ?? isbn10 ?? entity.Isbn;
                    }
                    break;
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _bookService.UpdateBookAsync(entity);

        return new JsonResult(new { success = true });
    }
}
