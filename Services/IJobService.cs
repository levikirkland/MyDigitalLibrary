using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Services;

public interface IJobService
{
    Task<JobEntity?> GetJobByBullMqIdAsync(string jobId);
    Task<JobEntity?> TryMarkInProgressAsync(string externalJobId); // atomic claim for Service Bus
    Task<JobEntity> CreateJobAsync(JobEntity job);
    Task UpdateJobStatusAsync(JobEntity job, string status, int? progress = null, string? error = null);

    // Added for UI/polling
    Task<JobEntity?[]> GetJobsByUserIdAsync(int userId);
    Task<JobEntity?> GetJobByJobIdAsync(string jobId);
}
