using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace MyDigitalLibrary.Core.Pages.Admin.Users;

[Authorize(Roles = "admin")]
public class EditModel : PageModel
{
    private readonly IAdminService _admin;
    private readonly IFeatureService _featureService;

    public EditModel(IAdminService admin, IFeatureService featureService) => (_admin, _featureService) = (admin, featureService);

    [BindProperty]
    public UserEntity? User { get; set; }

    public List<FeatureEntity> Features { get; set; } = new List<FeatureEntity>();

    // feature names to ensure visible in UI
    private static readonly string[] KnownFeatures = new[] { "google_import" };

    public async Task<IActionResult> OnGetAsync(int id)
    {
        User = await _admin.GetUserAsync(id);
        if (User == null) return NotFound();

        // Ensure global feature rows exist (safe no-op if table missing)
        foreach (var fname in KnownFeatures)
        {
            try { await _featureService.EnsureFeatureExistsAsync(fname); } catch { }
        }

        var feats = (await _featureService.GetFeaturesForUserAsync(id)).ToList();

        // Ensure known features are present in the list so UI always shows them
        foreach (var fname in KnownFeatures)
        {
            if (!feats.Any(f => string.Equals(f.Name, fname, StringComparison.OrdinalIgnoreCase)))
            {
                feats.Add(new FeatureEntity { UserId = id, Name = fname, Enabled = false });
            }
        }

        Features = feats.OrderBy(f => f.Name).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Ensure we have the user id even if model binding didn't populate the bound User
        if ((User == null || User.Id == 0))
        {
            var idStr = Request.Form["User.Id"].FirstOrDefault() ?? Request.Form["id"].FirstOrDefault();
            if (int.TryParse(idStr, out var parsedId))
            {
                User = await _admin.GetUserAsync(parsedId);
            }
        }

        if (User == null) return BadRequest();

        // Update role and active flag and display name
        await _admin.UpdateUserRoleAsync(User.Id, User.Role ?? "user");
        await _admin.ToggleUserActiveAsync(User.Id, User.IsActive);
        await _admin.UpdateUserDisplayNameAsync(User.Id, User.DisplayName);

        // Persist posted feature checkboxes: keys named feature_{name}
        var posted = Request.Form.Keys.Where(k => k.StartsWith("feature_"));
        foreach (var k in posted)
        {
            var name = k.Substring("feature_".Length);
            var enabled = Request.Form.ContainsKey(k);
            await _featureService.SetFeatureForUserAsync(User.Id, name, enabled);
        }

        TempData["Message"] = "User updated.";
        return RedirectToPage("/Admin/Index");
    }
}
