namespace MyDigitalLibrary.Core.Models
{
    public class Job
    {
        public int Id { get; set; }
        public string ExternalJobId { get; set; } = string.Empty; // maps JobEntity.JobId (queue id)
        public string JobType { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int? BookId { get; set; }
        public string? Status { get; set; }
        public int? Progress { get; set; }
        public string? Error { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
