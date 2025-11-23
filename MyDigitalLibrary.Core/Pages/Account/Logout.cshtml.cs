using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyDigitalLibrary.Core.Pages.Account;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync("Cookies");
        // also sign out from other schemes if used
        try { await HttpContext.SignOutAsync(); } catch { }
        return RedirectToPage("/Index");
    }
}
