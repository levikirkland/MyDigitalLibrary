using Microsoft.AspNetCore.Mvc;
using MyDigitalLibrary.Core.Data;

namespace MyDigitalLibrary.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public HealthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? _config["ASPNETCORE_ENVIRONMENT"] ?? "(not set)";
        var conn = _config["AZURE_SQL_CONNECTIONSTRING"] ?? _config.GetConnectionString("Default") ?? "(not set)";
        var masked = Mask(conn);

        bool canConnect = false;
        string? error = null;
        try
        {
            // Use CanConnectAsync to test DB connectivity without performing queries
            canConnect = await _db.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        return Ok(new
        {
            environment = env,
            database = new
            {
                configured = conn != "(not set)",
                preview = masked,
                canConnect,
                error
            }
        });
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "(not set)") return "(not set)";
        var len = value.Length;
        var prefix = value.Length <= 10 ? value : value.Substring(0, 10);
        var suffix = value.Length <= 14 ? "" : value.Substring(value.Length - 4);
        return prefix + "..." + suffix + " (len=" + len + ")";
    }
}
