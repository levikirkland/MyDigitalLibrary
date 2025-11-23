using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Repositories;
using MyDigitalLibrary.Services;
using MyDigitalLibrary.Entities;
using MyDigitalLibrary.Models;

namespace MyDigitalLibrary.Pages.Collections;

public class ViewModel : PageModel
{
    private readonly ICollectionRepository _repo;

    public ViewModel(ICollectionRepository repo)
    {
        _repo = repo;
    }

    public CollectionEntity? Collection { get; set; }
    public Book[]? Books { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var c = await _repo.GetCollectionByIdAsync(id);
        if (c == null || c.UserId != userId) return Forbid();
        Collection = c;

        // Manual members only: fetch book ids from BookCollections and then fetch user books
        var members = await _repo.GetBooksInCollectionAsync(id);
        var bookIds = members.Select(m => m.BookId).ToArray();

        var bookService = HttpContext.RequestServices.GetRequiredService<IBookService>();
        var userBooks = await bookService.GetBooksByUserIdAsync(userId);
        Books = userBooks.Where(b => bookIds.Contains(b.Id)).ToArray();

        return Page();
    }

    public string FormatDateShort(string? dateString) => dateString == null ? string.Empty : DateTime.TryParse(dateString, out var dt) ? dt.ToString("MMM d, yyyy") : dateString ?? string.Empty;
    public string FormatDate(string? dateString) => FormatDateShort(dateString);
    public string FormatBytes(long size) => size < 1024 ? size + " B" : size < 1048576 ? (size/1024.0).ToString("0.0") + " KB" : (size/1048576.0).ToString("0.0") + " MB";
}
