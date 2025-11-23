using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Entities;

[Table("reviews")]
public class ReviewEntity
{
    [Key]
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
