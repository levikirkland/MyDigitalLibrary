using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Entities;

[Table("collections")]
public class CollectionEntity
{
    [Key]
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSmart { get; set; }
    public string? Rules { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
