using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User, string? Token)> Register(string email, string password);
    Task<(bool Success, string? Error, User? User, string? Token)> Login(string email, string password);
    User? GetUserById(int userId);
    int? ValidateToken(string token);
}
