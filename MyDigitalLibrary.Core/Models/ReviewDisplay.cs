namespace MyDigitalLibrary.Core.Models;

public class ReviewDisplay
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
