using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Services;

namespace MyDigitalLibrary.Core.Pages.Books;

public class ViewModel : PageModel
{
    private readonly IBookService _bookService;

    public ViewModel(IBookService bookService)
    {
        _bookService = bookService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Book? Book { get; set; }
    public Book[]? SimilarBooks { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Book = await _bookService.GetBookByIdAsync(Id);
        if (Book == null)
        {
            Response.StatusCode = 404;
            return Page();
        }

        // TODO: fetch similar books (placeholder uses latest books for the same user)
        SimilarBooks = await _bookService.GetBooksByUserIdAsync(Book.UserId);
        return Page();
    }

    public string FormatDateShort(string? dateString) => dateString == null ? string.Empty : DateTime.TryParse(dateString, out var dt) ? dt.ToString("MMM d, yyyy") : dateString ?? string.Empty;
    public string FormatDate(string? dateString) => FormatDateShort(dateString);
    public string FormatBytes(long size) => size < 1024 ? size + " B" : size < 1048576 ? (size / 1024.0).ToString("0.0") + " KB" : (size / 1048576.0).ToString("0.0") + " MB";

    // Build a tag link that matches the book list filtering (SelectedTags query parameter)
    public string GetTagLink(string tag) => $"/Books?SelectedTags={Uri.EscapeDataString(tag)}";
}
