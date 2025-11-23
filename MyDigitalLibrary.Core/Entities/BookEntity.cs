using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Entities;

[Table("books")]
public class BookEntity
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int UserId { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Authors { get; set; }
    public string? Description { get; set; }
    [Required]
    public string OriginalFilename { get; set; } = string.Empty;
    [Required]
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? CoverPath { get; set; }
    public int? FileId { get; set; }
    public int? CoverFileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Extended metadata (not all projects may persist these yet) - added for edit UI
    public string? Publisher { get; set; }
    public string? Isbn { get; set; }
    public string? PublishedAt { get; set; }
    public string? Language { get; set; }
    public string? Series { get; set; }
    public Int16? SeriesIndex { get; set; }
    public byte? Rating { get; set; }
    public string? Tags { get; set; }

    // Reading progress
    public string? Status { get; set; }
    public byte? ProgressPercent { get; set; }
    public int? CurrentPage { get; set; }
    public int? TotalPages { get; set; }
    public string? StartedAt { get; set; }
    public string? FinishedAt { get; set; }
}
