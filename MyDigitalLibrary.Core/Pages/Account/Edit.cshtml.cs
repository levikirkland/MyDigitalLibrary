using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Pages.Account;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFeatureService _featureService;

    public EditModel(AppDbContext db, IFeatureService featureService) => (_db, _featureService) = (db, featureService);

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public bool ShareReviews { get; set; }

    [BindProperty]
    public bool GoogleImport { get; set; }

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        DisplayName = user.DisplayName;
        ShareReviews = user.ShareReviews;

        var feats = await _featureService.GetFeaturesForUserAsync(userId);
        GoogleImport = feats.Any(f => f.Name == "google_import" && f.Enabled);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.DisplayName = DisplayName;
        user.ShareReviews = ShareReviews;
        user.UpdatedAt = DateTime.UtcNow;

        // Persist user changes
        await _db.SaveChangesAsync();

        // Persist feature
        await _featureService.EnsureFeatureExistsAsync("google_import");
        await _featureService.SetFeatureForUserAsync(userId, "google_import", GoogleImport);

        Message = "Profile updated.";
        return RedirectToPage();
    }
}
