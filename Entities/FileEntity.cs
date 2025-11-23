using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Entities;

[Table("files")]
public class FileEntity
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string Sha256 { get; set; } = string.Empty;
    [Required]
    public string StoragePath { get; set; } = string.Empty;
    public long? Size { get; set; }
    public int RefCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
