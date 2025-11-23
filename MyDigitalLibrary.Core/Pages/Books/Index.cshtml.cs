using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Services;
using System.Security.Claims;

namespace MyDigitalLibrary.Core.Pages.Books;

public class IndexModel : PageModel
{
    private readonly IBookService _bookService;

    public IndexModel(IBookService bookService)
    {
        _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
    }

    public List<Book> Books { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string SearchQuery { get; set; }

    [BindProperty(SupportsGet = true)]
    public string[] SelectedTags { get; set; } = Array.Empty<string>();

    public List<string> AllTags { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        Book[] results;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            results = await _bookService.SearchBooksByUserIdAsync(userId, SearchQuery);
        else
            results = await _bookService.GetBooksByUserIdAsync(userId);

        Books = results.ToList();

        BuildTags();
        ApplyTagFilter();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var idClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        await _bookService.DeleteBookAsync(id, userId);
        return RedirectToPage();
    }

    private void BuildTags()
    {
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
        AllTags = tagSet.OrderBy(t => t).ToList();
    }

    private void ApplyTagFilter()
    {
        if (SelectedTags != null && SelectedTags.Length > 0)
        {
            Books = Books.Where(b =>
            {
                if (string.IsNullOrEmpty(b.Tags)) return false;
                var bookTags = b.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
                return SelectedTags.All(st => bookTags.Contains(st, StringComparer.OrdinalIgnoreCase));
            }).ToList();
        }
    }

    public string GetTagLink(string tag)
    {
        var selected = new List<string>(SelectedTags ?? Array.Empty<string>());
        if (selected.Contains(tag, StringComparer.OrdinalIgnoreCase))
            selected.RemoveAll(s => string.Equals(s, tag, StringComparison.OrdinalIgnoreCase));
        else
            selected.Add(tag);

        var url = Url.Page("./Index");
        if (selected.Count > 0)
        {
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);
            if (!string.IsNullOrWhiteSpace(SearchQuery)) qs["SearchQuery"] = SearchQuery;
            foreach (var s in selected) qs.Add("SelectedTags", s);
            return url + "?" + qs.ToString();
        }

        return url + (string.IsNullOrWhiteSpace(SearchQuery) ? "" : "?SearchQuery=" + System.Web.HttpUtility.UrlEncode(SearchQuery));
    }

    public string FormatDateShort(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return "-";
        if (DateTime.TryParse(dateString, out var d)) return d.ToShortDateString();
        return dateString;
    }
}
