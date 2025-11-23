using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Models;
using System.Security.Claims;

namespace MyDigitalLibrary.Core.Pages.Discover;

public class IndexModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IReviewService _reviewService;

    public IndexModel(IBookService bookService, IReviewService reviewService)
    {
        _bookService = bookService;
        _reviewService = reviewService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public List<Book> Books { get; set; } = new();
    public List<Book> Popular { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public async Task OnGetAsync()
    {
        // For Discover we surface recent books from all users. For now, use books from repository filtered by query.
        // As there's no public book flag, we'll re-use user's books across the system for demo purposes.

        // Simple approach: search each user's books? For demo use, fetch latest books from first user (placeholder)
        var idClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Book[] list;
        if (!string.IsNullOrWhiteSpace(Query) && int.TryParse(idClaim, out var uid))
        {
            // Search within user's library as placeholder
            list = await _bookService.SearchBooksByUserIdAsync(uid, Query);
        }
        else if (int.TryParse(idClaim, out var u))
        {
            list = await _bookService.GetBooksByUserIdAsync(u);
        }
        else
        {
            // no user - just return empty
            list = Array.Empty<Book>();
        }

        Books = list.OrderByDescending(b => b.CreatedAt).ToList();

        // Popular: Top rated books in the results
        Popular = Books.Where(b => b.Rating.HasValue).OrderByDescending(b => b.Rating).Take(5).ToList();

        // Tags: collect tags from results
        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in Books)
        {
            if (!string.IsNullOrWhiteSpace(b.Tags))
            {
                foreach (var t in b.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = t.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) tagSet.Add(trimmed);
                }
            }
        }
        Tags = tagSet.OrderBy(t => t).ToList();
    }
}
