namespace MyDigitalLibrary.Core.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? DisplayName { get; set; }
    public string? Features { get; set; }
    public bool ShareReviews { get; set; }
}
