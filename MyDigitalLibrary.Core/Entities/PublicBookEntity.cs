using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Entities;

[Table("public_books")]
public class PublicBookEntity
{
    [Key]
    public int Id { get; set; }
    public int BookId { get; set; }
    public int ReviewId { get; set; }

    // Non-sensitive public metadata
    public string Title { get; set; } = string.Empty;
    public string? Authors { get; set; }
    public string? CoverPath { get; set; }
    public string? Publisher { get; set; }
    public string? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
