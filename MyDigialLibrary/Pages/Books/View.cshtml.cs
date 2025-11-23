using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;
using MyDigitalLibrary.Models;
using MyDigitalLibrary.Repositories;
using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Pages.Books;

public class ViewModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly ICollectionRepository _collectionRepo;

    public ViewModel(IBookService bookService, ICollectionRepository collectionRepo)
    {
        _bookService = bookService;
        _collectionRepo = collectionRepo;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Book? Book { get; set; }
    public Book[]? SimilarBooks { get; set; }

    // Collections available to the current user (for Add to collection UI)
    public CollectionEntity[]? UserCollections { get; set; }

    // Collections that already contain this book (for display)
    public CollectionEntity[]? CollectionsContainingBook { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Book = await _bookService.GetBookByIdAsync(Id);
        if (Book == null)
        {
            Response.StatusCode = 404;
            return Page();
        }

        // fetch user's collections for dropdown and determine which contain this book
        var idClaim = User.FindFirst("userId")?.Value;
        if (int.TryParse(idClaim, out var userId))
        {
            UserCollections = await _collectionRepo.GetCollectionsByUserIdAsync(userId);

            var contains = new List<CollectionEntity>();
            if (UserCollections != null)
            {
                foreach (var c in UserCollections)
                {
                    var members = await _collectionRepo.GetBooksInCollectionAsync(c.Id);
                    if (members.Any(m => m.BookId == Id)) contains.Add(c);
                    if (contains.Count >= 3) break; // limit for display
                }
            }

            CollectionsContainingBook = contains.ToArray();
        }
        else
        {
            // ensure not null to simplify view logic
            UserCollections = Array.Empty<CollectionEntity>();
            CollectionsContainingBook = Array.Empty<CollectionEntity>();
        }

        // TODO: fetch similar books (placeholder uses latest books for the same user)
        SimilarBooks = await _bookService.GetBooksByUserIdAsync(Book.UserId);
        return Page();
    }

    // POST handler to add book to user's collection (no API call required)
    public async Task<IActionResult> OnPostAddToCollectionAsync(int collectionId)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return Unauthorized();

        // verify collection belongs to user
        var c = await _collectionRepo.GetCollectionByIdAsync(collectionId);
        if (c == null || c.UserId != userId) return Forbid();

        await _collectionRepo.AddBookAsync(new BookCollectionEntity { CollectionId = collectionId, BookId = Id });

        // redirect back to view so the UI refreshes server-side
        return RedirectToPage();
    }

    public string FormatDateShort(string? dateString) => dateString == null ? string.Empty : DateTime.TryParse(dateString, out var dt) ? dt.ToString("MMM d, yyyy") : dateString ?? string.Empty;
    public string FormatDate(string? dateString) => FormatDateShort(dateString);
    public string FormatBytes(long size) => size < 1024 ? size + " B" : size < 1048576 ? (size/1024.0).ToString("0.0") + " KB" : (size/1048576.0).ToString("0.0") + " MB";

    // Build a tag link that matches the book list filtering (SelectedTags query parameter)
    public string GetTagLink(string tag) => $"/Books?SelectedTags={Uri.EscapeDataString(tag)}";
}
