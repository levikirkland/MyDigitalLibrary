using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Data;

namespace MyDigitalLibrary.Core.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _db;

    public IndexModel(IAuthService authService, AppDbContext db)
    {
        _authService = authService;
        _db = db;
    }

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public bool ShareReviews { get; set; }

    [TempData]
    public string? Message { get; set; }

    public void OnGet()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId))
        {
            RedirectToPage("/Account/Login");
            return;
        }

        var entity = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (entity == null)
        {
            Message = "User not found.";
            return;
        }

        DisplayName = entity.DisplayName;
        ShareReviews = entity.ShareReviews;
    }

    public IActionResult OnPost()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var entity = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (entity == null)
        {
            Message = "User not found.";
            return Page();
        }

        entity.DisplayName = DisplayName;
        entity.ShareReviews = ShareReviews;
        entity.UpdatedAt = DateTime.UtcNow;
        _db.SaveChanges();

        Message = "Settings saved.";
        return Page();
    }
}