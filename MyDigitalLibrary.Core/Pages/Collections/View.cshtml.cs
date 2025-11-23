using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Core.Pages.Collections;

public class ViewModel : PageModel
{
    private readonly ICollectionService _svc;
    private readonly IBookService _bookService;

    public ViewModel(ICollectionService svc, IBookService bookService)
    {
        _svc = svc;
        _bookService = bookService;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public CollectionEntity? Collection { get; set; }
    public Book[]? Books { get; set; }

    // raw membership rows for diagnostics
    public Core.Entities.BookCollectionEntity[]? Members { get; set; }
    public int? MemberCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var c = await _svc.GetCollectionAsync(Id);
        if (c == null || c.UserId != userId) return Forbid();
        Collection = c;

        // get membership rows and count for diagnostics
        var members = await _svc.GetBooksInCollectionAsync(c.Id);
        Members = members;
        MemberCount = await _svc.GetBookCountAsync(c.Id);

        // If smart, evaluate rules; otherwise, list manual members
        if (c.IsSmart)
        {
            var rules = await _svc.GetRulesForCollectionAsync(c.Id);
            var ruleModels = rules.Select(r => new Rule { ColumnName = r.RuleType.Equals("status", StringComparison.OrdinalIgnoreCase) ? "Status" : r.RuleType.Equals("rating", StringComparison.OrdinalIgnoreCase) ? "Rating" : r.RuleType.Equals("series", StringComparison.OrdinalIgnoreCase) ? "Series" : "Tags", Operator = r.RuleType.Equals("tag", StringComparison.OrdinalIgnoreCase) ? RuleOperator.Like : RuleOperator.Equals, Value = r.RuleValue }).ToArray();
            Books = await _bookService.GetBooksByRulesAsync(ruleModels, userId);
        }
        else
        {
            var ids = members.Select(m => m.BookId).ToArray();
            if (ids.Length == 0) { Books = Array.Empty<Book>(); }
            else
            {
                var bookModels = await _bookService.GetBooksByIdsAsync(ids);
                // preserve order of ids
                var map = bookModels.ToDictionary(b => b.Id);
                Books = ids.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToArray();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveBookAsync(int bookId)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var c = await _svc.GetCollectionAsync(Id);
        if (c == null || c.UserId != userId) return Forbid();

        await _svc.RemoveBookFromCollectionAsync(c.Id, bookId);
        return RedirectToPage(new { id = c.Id });
    }

    // Diagnostic: check membership vs existing books
    public async Task<JsonResult> OnPostCheckMembersAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return new JsonResult(new { error = "unauthorized" }) { StatusCode = 401 };

        var c = await _svc.GetCollectionAsync(Id);
        if (c == null || c.UserId != userId) return new JsonResult(new { error = "forbidden" }) { StatusCode = 403 };

        var members = await _svc.GetBooksInCollectionAsync(c.Id);
        var ids = members.Select(m => m.BookId).ToArray();
        var found = await _bookService.GetBooksByIdsAsync(ids);
        var foundIds = found.Select(b => b.Id).ToArray();
        var missing = ids.Except(foundIds).ToArray();

        return new JsonResult(new { memberCount = ids.Length, memberRows = members.Select(m => new { m.BookId, m.AddedAt }), foundCount = foundIds.Length, foundIds, missing });
    }
}
