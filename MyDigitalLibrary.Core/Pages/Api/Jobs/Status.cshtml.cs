using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;

namespace MyDigitalLibrary.Core.Pages.Api.Jobs;

// Lightweight API endpoint page that returns JSON status for a job id
public class StatusModel : PageModel
{
    private readonly IJobService _jobService;

    public StatusModel(IJobService jobService)
    {
        _jobService = jobService;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("jobId required");
        var job = await _jobService.GetJobByJobIdAsync(id);
        if (job == null) return NotFound();

        return new JsonResult(new { jobId = job.JobId, status = job.Status, progress = job.Progress ?? 0, error = job.Error });
    }
}
