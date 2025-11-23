using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Entities;

[Table("collection_rules")]
public class CollectionRuleEntity
{
    [Key]
    public int Id { get; set; }
    public int CollectionId { get; set; }
    [Required]
    public string RuleType { get; set; } = string.Empty; // e.g. status, rating, tag, series
    [Required]
    public string RuleValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
