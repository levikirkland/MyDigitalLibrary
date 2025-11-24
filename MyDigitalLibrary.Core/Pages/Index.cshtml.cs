using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace MyDigitalLibrary.Core.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;
        public IndexModel(IConfiguration config) => _config = config;

        public string EnvironmentName { get; private set; } = string.Empty;
        public bool HasAzureSqlConnection { get; private set; }
        public string AzureSqlPreview { get; private set; } = "";
        public bool HasDefaultConnection { get; private set; }
        public string DefaultConnectionPreview { get; private set; } = "";

        public IActionResult OnGet()
        {
            EnvironmentName = (_config["ASPNETCORE_ENVIRONMENT"] ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(not set)");

            var azure = _config["AZURE_SQL_CONNECTIONSTRING"];
            HasAzureSqlConnection = !string.IsNullOrWhiteSpace(azure);
            AzureSqlPreview = MaskSecret(azure);

            var def = _config.GetConnectionString("Default");
            HasDefaultConnection = !string.IsNullOrWhiteSpace(def);
            DefaultConnectionPreview = MaskSecret(def);

            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Books/Index");
            }

            return Page();
        }

        private static string MaskSecret(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "(not set)";
            // show a short prefix and suffix only
            var prefix = value.Length <= 10 ? value : value.Substring(0, 10);
            var suffix = value.Length <= 14 ? "" : value.Substring(value.Length - 4);
            return prefix + "..." + suffix + " (len=" + value.Length + ")";
        }
    }
}
