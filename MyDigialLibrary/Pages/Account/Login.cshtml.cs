using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class LoginModel : PageModel
{
    private readonly IAuthService _auth;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IAuthService auth, ILogger<LoginModel> logger)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [BindProperty]
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [BindProperty]
    [Required]
    public string? Password { get; set; }

    [BindProperty]
    public bool RememberMe { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            _logger.LogInformation("Login failed due to invalid model state: {ModelState}", ModelState);
            ViewData["Error"] = "Please correct the form errors.";
            return Page();
        }

        try
        {
            var result = await _auth.Login(Email!, Password!);
            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                // Read claims from the JWT and sign in the cookie principal
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(result.Token);
                var claims = jwtToken.Claims.ToList();

                // Ensure there is at least a name claim for display (fallback to email)
                if (!claims.Any(c => c.Type == ClaimTypes.Name) && !string.IsNullOrEmpty(result.User?.Email))
                {
                    claims.Add(new Claim(ClaimTypes.Name, result.User.Email));
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var principal = new ClaimsPrincipal(identity);

                // Make cookie persistent so it survives browser restarts / app restarts
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddDays(14)
                };

                await HttpContext.SignInAsync("Cookies", principal, authProperties);

                // Redirect to the library page
                return RedirectToPage("/Books/Index");
            }

            _logger.LogInformation("Login failed for {Email}: {Error}", Email, result.Error);
            ViewData["Error"] = result.Error ?? "Login failed";
            ModelState.AddModelError(string.Empty, result.Error ?? "Login failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during login for {Email}", Email);
            ViewData["Error"] = "An error occurred while trying to log in.";
            ModelState.AddModelError(string.Empty, "Login failed");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostLogout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/");
    }
}