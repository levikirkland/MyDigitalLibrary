using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;

namespace MyDigitalLibrary.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IAuthService _auth;

        public RegisterModel(IAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        [BindProperty]
        public string PasswordConfirm { get; set; }

        [BindProperty]
        public bool AcceptedTerms { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Password != PasswordConfirm)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return Page();
            }

            if (!AcceptedTerms)
            {
                ModelState.AddModelError(string.Empty, "You must accept the Terms of Use to register.");
                return Page();
            }

            try
            {
                var result = await _auth.Register(Email, Password);
                if (result.Success)
                {
                    return RedirectToPage("/Account/Login");
                }

                ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed. Please try again.");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"An error occurred: {ex.Message}");
            }

            return Page();
        }
    }
}
