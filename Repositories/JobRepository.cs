using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Data;
using MyDigitalLibrary.Entities;
using System.Data;

namespace MyDigitalLibrary.Repositories;

public class JobRepository : IJobRepository
{
    private readonly AppDbContext _db;
    public JobRepository(AppDbContext db) => _db = db;

    public async Task<JobEntity?> GetByBullMqIdAsync(string jobId)
    {
        return await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
    }

    // Claim job by external id using EF transaction. Note: this uses a transactional select+update; for stronger
    // guarantees replace with provider-specific UPDATE...OUTPUT as needed.
    public async Task<JobEntity?> TryMarkInProgressByExternalIdAsync(string externalJobId)
    {
        if (string.IsNullOrEmpty(externalJobId)) return null;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == externalJobId);
            if (job == null) return null;

            // Only claim if queued/pending or unset
            var status = job.Status?.ToLowerInvariant();
            if (status != null && status != "queued" && status != "pending")
            {
                return null; // already claimed or completed
            }

            job.Status = "in-progress";
            job.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return job;
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { /* swallow */ }
            throw;
        }
    }

    public async Task<JobEntity> AddAsync(JobEntity job)
    {
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task UpdateAsync(JobEntity job)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync();
    }

    // Jobs UI methods
    public async Task<JobEntity?[]> GetJobsByUserIdAsync(int userId)
    {
        var jobs = await _db.Jobs.Where(j => j.UserId == userId).OrderByDescending(j => j.CreatedAt).ToArrayAsync();
        return jobs.Select(j => (JobEntity?)j).ToArray();
    }

    public async Task<JobEntity?> GetByJobIdAsync(string jobId)
    {
        return await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
    }
}