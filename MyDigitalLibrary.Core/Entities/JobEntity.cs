using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Entities
{
    [Table("jobs")]
    public class JobEntity
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int? BookId { get; set; }

        [Required]
        public string JobType { get; set; } = string.Empty;

        // External queue id (e.g. BullMQ / ServiceBus job id)
        [Required]
        public string JobId { get; set; } = string.Empty;

        public string? Status { get; set; }

        public int? Progress { get; set; }

        public string? Error { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Note: uploaded import files are stored on disk under uploads/imports/{JobId}; worker derives path from JobId
    }
}