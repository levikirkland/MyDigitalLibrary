using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyDigitalLibrary.Core.Middleware;

public class LockScreenMiddleware
{
    private readonly RequestDelegate _next;

    public LockScreenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only consider authenticated users
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path;
            // If locked cookie present and not already on account lock/unlock paths and not an API/static resource
            var locked = context.Request.Cookies.ContainsKey("ScreenLocked");
            if (locked && !path.StartsWithSegments("/Account/Lock", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/Account/Unlock", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) && !path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/Account/Lock");
                return;
            }
        }

        await _next(context);
    }
}
