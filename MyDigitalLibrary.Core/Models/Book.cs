namespace MyDigitalLibrary.Core.Models
{
    public class Book
    {
        // persisted fields (match BookEntity)
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Authors { get; set; }
        public string? Description { get; set; }
        public string OriginalFilename { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? CoverPath { get; set; }
        public int? FileId { get; set; }
        public int? CoverFileId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Extended metadata (nullable to reflect DB)
        public string? Publisher { get; set; }
        public string? Isbn { get; set; }
        public string? PublishedAt { get; set; }
        public string? Language { get; set; }
        public string? Series { get; set; }
        public int? SeriesIndex { get; set; }
        public byte? Rating { get; set; }
        public string? Tags { get; set; }

        // Reading progress
        public string? Status { get; set; }
        public byte? ProgressPercent { get; set; }
        public int? CurrentPage { get; set; }
        public int? TotalPages { get; set; }
        public string? StartedAt { get; set; }
        public string? FinishedAt { get; set; }

        // Cover URL for views: return the public thumbnail URI when available
        public string? CoverUrl => string.IsNullOrEmpty(CoverPath) ? null : CoverPath;
    }
}