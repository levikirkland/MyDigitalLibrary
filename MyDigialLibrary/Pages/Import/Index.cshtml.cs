using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;
using System.IO;
using System.Linq;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace MyDigitalLibrary.Pages.Import;

public class IndexModel : PageModel
{
    private readonly CalibreImporter _importer;
    private readonly ILogger<IndexModel> _logger;
    private readonly IJobService _jobService;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public IndexModel(CalibreImporter importer, ILogger<IndexModel> logger, IJobService jobService, IConfiguration config, IHostEnvironment env)
    {
        _importer = importer;
        _logger = logger;
        _jobService = jobService;
        _config = config;
        _env = env;
    }

    [BindProperty]
    public bool ImportCovers { get; set; } = true;

    public string? ResultMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        if (Request.Form.Files == null || Request.Form.Files.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Please upload a Calibre folder (use the directory picker) to import.");
            return Page();
        }

        // create job record first so we have jobId for target folder
        var job = new MyDigitalLibrary.Entities.JobEntity
        {
            UserId = userId,
            JobType = "import",
            JobId = Guid.NewGuid().ToString(),
            Status = "queued",
            Progress = 0
        };

        var created = await _jobService.CreateJobAsync(job);

        // Target upload location under app content root so worker can access
        var targetRoot = Path.Combine(_env.ContentRootPath, "uploads", "imports", created.JobId);
        Directory.CreateDirectory(targetRoot);

        try
        {
            foreach (var formFile in Request.Form.Files)
            {
                // Browser may send relative path in file name (webkitdirectory). Normalize safely.
                var relative = formFile.FileName.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
                var parts = relative.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var safeParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                if (safeParts.Length == 0) continue;

                var targetPath = Path.Combine(new[] { targetRoot }.Concat(safeParts).ToArray());
                var targetDir = Path.GetDirectoryName(targetPath)!;
                Directory.CreateDirectory(targetDir);

                await using var stream = System.IO.File.Create(targetPath);
                await formFile.CopyToAsync(stream);
            }

            // Write metadata file so worker can read import options without DB schema changes
            var meta = JsonSerializer.Serialize(new { importCovers = ImportCovers });
            await System.IO.File.WriteAllTextAsync(Path.Combine(targetRoot, "import.json"), meta);

            await _jobService.UpdateJobStatusAsync(created, "queued", 0, null);

            var sbConn = _config["AZURE_SERVICEBUS_CONNECTIONSTRING"] ?? _config["SERVICEBUS_CONNECTIONSTRING"] ?? string.Empty;
            var queueName = _config["AZURE_SERVICEBUS_QUEUE_NAME"] ?? _config["SERVICEBUS_QUEUE_NAME"] ?? _config["AZURE_QUEUE_NAME"] ?? "bookshelfworker";

            if (!string.IsNullOrEmpty(sbConn))
            {
                // send message to ServiceBus with jobId so WorkerService picks it up
                await using var client = new ServiceBusClient(sbConn);
                var sender = client.CreateSender(queueName);
                var messageBody = JsonSerializer.Serialize(new { jobId = created.JobId });
                var msg = new ServiceBusMessage(messageBody);
                await sender.SendMessageAsync(msg);

                ResultMessage = $"Import job {created.JobId} queued. Worker will process it.";
            }
            else
            {
                // No ServiceBus -> run import in-process background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _jobService.UpdateJobStatusAsync(created, "in-progress", 0, null);
                        var (imported, skipped) = await _importer.ImportFromDirectoryAsync(targetRoot, userId, ImportCovers, CancellationToken.None);
                        var summary = $"Imported {imported}, skipped {skipped}.";
                        await _jobService.UpdateJobStatusAsync(created, "completed", 100, summary);
                    }
                    catch (Exception ex)
                    {
                        try { await _jobService.UpdateJobStatusAsync(created, "failed", created.Progress, ex.Message); } catch { }
                        _logger.LogError(ex, "Background import failed for job {JobId}", created.JobId);
                    }
                    finally
                    {
                        try { Directory.Delete(targetRoot, true); } catch { }
                    }
                });

                ResultMessage = $"Import job {created.JobId} queued and running in background. You can check Jobs for status.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            ModelState.AddModelError(string.Empty, "Import failed: " + ex.Message);
            try { Directory.Delete(targetRoot, true); } catch { }
        }

        return Page();
    }
}
