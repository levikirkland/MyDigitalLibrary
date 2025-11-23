using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace MyDigitalLibrary.Core.Pages.Account;

public class LockModel : PageModel
{
    public string? DisplayName { get; private set; }

    public void OnGet()
    {
        DisplayName = User?.Identity?.Name ?? User?.FindFirst(ClaimTypes.Email)?.Value ?? "User";

        // Prevent caching of the lock page so back button doesn't show protected content
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
    }

    public IActionResult OnPostSet()
    {
        // set lock cookie for current session (session cookie)
        Response.Cookies.Append("ScreenLocked", "1", new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });
        return RedirectToPage();
    }

    public IActionResult OnPostUnlock(string password)
    {
        // For simplicity, unlocking just clears the cookie; real application should re-authenticate.
        Response.Cookies.Delete("ScreenLocked");
        return RedirectToPage("/Books/Index");
    }
}
