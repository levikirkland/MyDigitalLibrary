using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Services
{
    public static class JobMapper
    {
        public static Job ToModel(this JobEntity e)
        {
            if (e == null) return null!; // caller should guard, or change to return Job?
            return new Job
            {
                Id = e.Id,
                ExternalJobId = e.JobId,
                JobType = e.JobType,
                UserId = e.UserId,
                BookId = e.BookId,
                Status = e.Status,
                Progress = e.Progress,
                Error = e.Error,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                CreatedAt = e.CreatedAt
            };
        }

        public static JobEntity ToEntity(this Job model)
        {
            return new JobEntity
            {
                Id = model.Id,
                JobId = model.ExternalJobId,
                JobType = model.JobType,
                UserId = model.UserId,
                BookId = model.BookId,
                Status = model.Status,
                Progress = model.Progress,
                Error = model.Error,
                StartedAt = model.StartedAt,
                CompletedAt = model.CompletedAt,
                CreatedAt = model.CreatedAt
            };
        }
    }
}
