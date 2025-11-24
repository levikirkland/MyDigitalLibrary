using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;

namespace MyDigitalLibrary.Core.Middleware;

public class ClaimsRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClaimsRefreshMiddleware> _log;

    public ClaimsRefreshMiddleware(RequestDelegate next, ILogger<ClaimsRefreshMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var user = context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var idClaim = user.FindFirst("userId")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var userId))
                {
                    using var scope = context.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetService<AppDbContext>();
                    if (db != null)
                    {
                        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                        var currentRole = user.FindFirst(ClaimTypes.Role)?.Value;
                        var dbRole = entity?.Role ?? string.Empty;
                        if (!string.Equals(currentRole ?? string.Empty, dbRole ?? string.Empty, StringComparison.Ordinal))
                        {
                            // Re-issue cookie with updated role claim
                            var claims = user.Claims.Where(c => c.Type != ClaimTypes.Role).ToList();
                            if (!string.IsNullOrEmpty(dbRole)) claims.Add(new Claim(ClaimTypes.Role, dbRole));

                            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                            var principal = new ClaimsPrincipal(identity);

                            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                            _log.LogInformation("Refreshed auth cookie for user {UserId} with role {Role}", userId, dbRole);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Claims refresh middleware encountered an error");
        }

        await _next(context);
    }
}
