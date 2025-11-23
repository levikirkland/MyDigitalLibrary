using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyDigitalLibrary.Core.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Books/Index");
            }

            return Page();
        }
    }
}
