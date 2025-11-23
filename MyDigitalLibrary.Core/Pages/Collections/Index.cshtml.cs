using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Repositories;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Pages.Collections;

public class IndexModel : PageModel
{
    private readonly ICollectionRepository _repo;

    public IndexModel(ICollectionRepository repo)
    {
        _repo = repo;
    }

    public CollectionEntity[] Collections { get; set; } = Array.Empty<CollectionEntity>();

    public async Task OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) { Collections = Array.Empty<CollectionEntity>(); return; }
        Collections = await _repo.GetCollectionsByUserIdAsync(userId);
    }

    // POST handler to create a new collection via form submit
    public async Task<IActionResult> OnPostCreateAsync([FromForm] string name, [FromForm] string? description)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Name is required.";
            return RedirectToPage();
        }

        var c = new CollectionEntity { UserId = userId, Name = name.Trim(), Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim() };
        await _repo.AddAsync(c);
        return RedirectToPage();
    }
}
