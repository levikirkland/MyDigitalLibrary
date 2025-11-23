using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Entities;

[Table("reading_progress")]
public class ReadingProgressEntity
{
  [Key]
  public int Id { get; set; }
  public int BookId { get; set; }
  public int UserId { get; set; }
  public string? Status { get; set; }
  public int? ProgressPercent { get; set; }
  public int? CurrentPage { get; set; }
  public int? TotalPages { get; set; }
  public DateTime? StartedAt { get; set; }
  public DateTime? FinishedAt { get; set; }
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

  // Last known EPUB CFI location (epubcfi(...)) for restoring reader position
  public string? LastLocation { get; set; }
}
