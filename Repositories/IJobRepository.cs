using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Repositories;

public interface IJobRepository
{
    Task<JobEntity?> GetByBullMqIdAsync(string jobId);
    Task<JobEntity?> TryMarkInProgressByExternalIdAsync(string externalJobId);
    Task<JobEntity> AddAsync(JobEntity job);
    Task UpdateAsync(JobEntity job);

    // Added for UI and worker
    Task<JobEntity?[]> GetJobsByUserIdAsync(int userId);
    Task<JobEntity?> GetByJobIdAsync(string jobId);
}