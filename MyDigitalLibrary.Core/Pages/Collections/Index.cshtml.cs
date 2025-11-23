using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Pages.Collections;

public class IndexModel : PageModel
{
    private readonly ICollectionService _svc;

    public IndexModel(ICollectionService svc)
    {
        _svc = svc;
    }

    public CollectionEntity[] Collections { get; set; } = Array.Empty<CollectionEntity>();
    public Dictionary<int,int> Counts { get; set; } = new Dictionary<int,int>();

    public async Task OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) { Collections = Array.Empty<CollectionEntity>(); return; }
        var list = (await _svc.GetCollectionsByUserAsync(userId)).ToArray();
        Collections = list;
        foreach (var c in list)
        {
            Counts[c.Id] = await _svc.GetBookCountAsync(c.Id);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");
        var c = await _svc.GetCollectionAsync(id);
        if (c == null || c.UserId != userId) return Forbid();
        await _svc.DeleteCollectionAsync(id);
        return RedirectToPage();
    }
}
