using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Services;

namespace MyDigitalLibrary.Pages.Jobs;

public class IndexModel : PageModel
{
    private readonly IJobService _jobService;
    private readonly ILogger<IndexModel> _logger;
    private readonly IHttpContextAccessor _accessor;

    public IndexModel(IJobService jobService, ILogger<IndexModel> logger, IHttpContextAccessor accessor)
    {
        _jobService = jobService;
        _logger = logger;
        _accessor = accessor;
    }

    public List<MyDigitalLibrary.Entities.JobEntity> Jobs { get; set; } = new List<MyDigitalLibrary.Entities.JobEntity>();

    public async Task OnGetAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return;

        // Use repository via service - add method to IJobService if needed. For now use jobService Get via repo.
        var repo = HttpContext.RequestServices.GetRequiredService<MyDigitalLibrary.Repositories.IJobRepository>();
        var arr = await repo.GetJobsByUserIdAsync(userId);
        Jobs = arr.Where(j => j != null).Select(j => j!).ToList();
    }
}
