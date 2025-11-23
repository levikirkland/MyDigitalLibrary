using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDigitalLibrary.Core.Core.Entities;

[Table("book_collections")]
public class BookCollectionEntity
{
    [Key]
    public int Id { get; set; }
    public int BookId { get; set; }
    public int CollectionId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
