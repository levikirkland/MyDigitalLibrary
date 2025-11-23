using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Entities;

[Table("formats")]
public class FormatEntity
{
    [Key]
    public int Id { get; set; }
    [Required]
    public int BookId { get; set; }
    [Required]
    public string Format { get; set; } = string.Empty;
    [Required]
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public int? FileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
