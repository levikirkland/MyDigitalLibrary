using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace MyDigitalLibrary.Core.Pages.Books;

public class ViewModel : PageModel
{
    private readonly IBookService _bookService;
    private readonly ICollectionService _collectionService;
    private readonly ILogger<ViewModel> _logger;
    private readonly IReviewService _reviewService;

    public ViewModel(IBookService bookService, ICollectionService collectionService, ILogger<ViewModel> logger, IReviewService reviewService)
    {
        _bookService = bookService;
        _collectionService = collectionService;
        _logger = logger;
        _reviewService = reviewService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Book? Book { get; set; }
    public Book[]? SimilarBooks { get; set; }

    // Collections for the current user (used in the Add to collection dropdown)
    public CollectionEntity[]? Collections { get; set; }

    [TempData]
    public string? Message { get; set; }

    // Reviews
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public ReviewEntity? UserReview { get; set; }
    public ReviewDisplay[]? Reviews { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Book = await _bookService.GetBookByIdAsync(Id);
        if (Book == null)
        {
            Response.StatusCode = 404;
            return Page();
        }

        int? currentUserId = null;
        var idClaim = User.FindFirst("userId")?.Value;
        if (int.TryParse(idClaim, out var userId))
        {
            currentUserId = userId;
            Collections = (await _collection_service_fix_GetCollections());
            // load the current user's review for this book
            UserReview = await _reviewService.GetUserReviewAsync(Book.Id, userId);
        }
        else
        {
            Collections = Array.Empty<CollectionEntity>();
            UserReview = null;
        }

        // TODO: fetch similar books (placeholder uses latest books for the same user)
        SimilarBooks = await _bookService.GetBooksByUserIdAsync(Book.UserId);

        // load reviews and average
        var avg = await _reviewService.GetAverageRatingAsync(Book.Id);
        AverageRating = avg.AverageRating;
        ReviewCount = avg.Count;
        Reviews = (await _reviewService.GetBookReviewsForDisplayAsync(Book.Id, currentUserId)).ToArray();

        return Page();
    }

    // Helper to call collection service with error handling and keep code compact in edit
    private async Task<CollectionEntity[]> _collection_service_fix_GetCollections()
    {
        try
        {
            return (await _collectionService.GetCollectionsByUserAsync(int.Parse(User.FindFirst("userId")?.Value ?? "0"))).ToArray();
        }
        catch
        {
            return Array.Empty<CollectionEntity>();
        }
    }

    public string FormatDateShort(string? dateString) => dateString == null ? string.Empty : DateTime.TryParse(dateString, out var dt) ? dt.ToString("MMM d, yyyy") : dateString ?? string.Empty;
    public string FormatDate(string? dateString) => FormatDateShort(dateString);
    public string FormatBytes(long size) => size < 1024 ? size + " B" : size < 1048576 ? (size / 1024.0).ToString("0.0") + " KB" : (size / 1048576.0).ToString("0.0") + " MB";

    // Build a tag link that matches the book list filtering (SelectedTags query parameter)
    public string GetTagLink(string tag) => $"/Books?SelectedTags={Uri.EscapeDataString(tag)}";

    public async Task<IActionResult> OnPostAddToCollectionAsync(int collectionId)
    {
        var bookId = Id; // use route id bound to page

        // ensure user signed in
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId))
        {
            Message = "You must be signed in to add to a collection.";
            return RedirectToPage(new { id = bookId });
        }

        if (collectionId <= 0)
        {
            Message = "Please select a collection.";
            return RedirectToPage(new { id = bookId });
        }

        var c = await _collectionService.GetCollectionAsync(collectionId);
        if (c == null || c.UserId != userId)
        {
            Message = "Collection not found or access denied.";
            return RedirectToPage(new { id = bookId });
        }

        try
        {
            await _collectionService.AddBookToCollectionAsync(collectionId, bookId);
            Message = "Added to collection.";
            _logger.LogInformation("Added book {BookId} to collection {CollectionId} by user {UserId}", bookId, collectionId, userId);
        }
        catch (InvalidOperationException ex)
        {
            // service uses InvalidOperationException to indicate duplicate membership
            Message = "Book already exists in collection.";
            _logger.LogInformation(ex, "Attempted to add duplicate book {BookId} to collection {CollectionId}", bookId, collectionId);
        }
        catch (DbUpdateException ex)
        {
            Message = "Database error when adding to collection.";
            _logger.LogError(ex, "DB error adding book {BookId} to collection {CollectionId}", bookId, collectionId);
        }
        catch (Exception ex)
        {
            Message = "Failed to add book to collection.";
            _logger.LogError(ex, "Unexpected error adding book {BookId} to collection {CollectionId}", bookId, collectionId);
        }

        return RedirectToPage(new { id = bookId });
    }

    // POST handler for submitting/updating a review
    public async Task<IActionResult> OnPostSubmitReviewAsync(int rating, string? reviewText, bool makePrivate = false)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        if (rating < 1 || rating > 5)
        {
            TempData["Message"] = "Rating must be between 1 and 5.";
            return RedirectToPage(new { id = Id });
        }

        try
        {
            var isPublic = !makePrivate;
            await _reviewService.UpsertReviewAsync(Id, userId, rating, reviewText, isPublic);
            TempData["Message"] = "Review saved.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save review for book {BookId} by user {UserId}", Id, userId);
            TempData["Message"] = "Failed to save review.";
        }

        return RedirectToPage(new { id = Id });
    }

    // POST handler for deleting current user's review
    public async Task<IActionResult> OnPostDeleteReviewAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        try
        {
            await _reviewService.DeleteReviewAsync(Id, userId);
            TempData["Message"] = "Review deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete review for book {BookId} by user {UserId}", Id, userId);
            TempData["Message"] = "Failed to delete review.";
        }

        return RedirectToPage(new { id = Id });
    }

    // POST handler to toggle review public flag
    public async Task<IActionResult> OnPostToggleReviewPublicAsync(int reviewId)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        try
        {
            var updated = await _reviewService.ToggleReviewPublicAsync(reviewId, userId);
            if (updated == null)
            {
                TempData["Message"] = "Review not found or access denied.";
            }
            else
            {
                TempData["Message"] = "Review visibility updated.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle review visibility for review {ReviewId} by user {UserId}", reviewId, userId);
            TempData["Message"] = "Failed to update review visibility.";
        }

        return RedirectToPage(new { id = Id });
    }
}
