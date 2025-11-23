using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Repositories;

namespace MyDigitalLibrary.Core.Services;

public class JobService : IJobService
{
    private readonly IJobRepository _repo;
    public JobService(IJobRepository repo) => _repo = repo;

    public Task<JobEntity?> GetJobByBullMqIdAsync(string jobId) => _repo.GetByBullMqIdAsync(jobId);

    public Task<JobEntity?> TryMarkInProgressAsync(string externalJobId) => _repo.TryMarkInProgressByExternalIdAsync(externalJobId);

    public Task<JobEntity> CreateJobAsync(JobEntity job) => _repo.AddAsync(job);

    public async Task UpdateJobStatusAsync(JobEntity job, string status, int? progress = null, string? error = null)
    {
        job.Status = status;
        if (progress.HasValue) job.Progress = progress;
        if (!string.IsNullOrEmpty(error)) job.Error = error;

        if (status == "completed")
        {
            job.Progress = 100;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (status == "in-progress")
        {
            job.StartedAt ??= DateTime.UtcNow;
        }

        await _repo.UpdateAsync(job);
    }

    public Task<JobEntity?[]> GetJobsByUserIdAsync(int userId) => _repo.GetJobsByUserIdAsync(userId);

    public Task<JobEntity?> GetJobByJobIdAsync(string jobId) => _repo.GetByJobIdAsync(jobId);
}
