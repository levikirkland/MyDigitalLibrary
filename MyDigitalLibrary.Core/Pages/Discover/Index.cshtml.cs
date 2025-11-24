using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Models;
using System.Security.Claims;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Core.Pages.Discover;

public class IndexModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly IReviewService _reviewService;
    private readonly AppDbContext _db;

    public IndexModel(IBookService bookService, IReviewService reviewService, AppDbContext db)
    {
        _bookService = bookService;
        _reviewService = reviewService;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public List<Book> Books { get; set; } = new();
    public List<Book> Popular { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public async Task OnGetAsync()
    {
        // If query provided, search public books by title/author/publisher
        IQueryable<PublicBookEntity> q = _db.PublicBooks.OrderByDescending(p => p.UpdatedAt).Take(50);

        if (!string.IsNullOrWhiteSpace(Query))
        {
            var term = Query.Trim();
            q = _db.PublicBooks.Where(p => EF.Functions.Like(p.Title, $"%{term}%") || EF.Functions.Like(p.Authors, $"%{term}%") || EF.Functions.Like(p.Publisher, $"%{term}%")).OrderByDescending(p => p.UpdatedAt).Take(50);
        }

        var list = await q.Select(p => new Book
        {
            Id = p.Id,
            Title = p.Title,
            Authors = p.Authors,
            CoverPath = p.CoverPath,
            Publisher = p.Publisher,
            PublishedAt = p.PublishedAt,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToListAsync();

        Books = list.OrderByDescending(b => b.CreatedAt).ToList();

        // Popular: Top rated books in the results (if rating present)
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
