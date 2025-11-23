using MyDigitalLibrary.Core.Models;
using MyDigitalLibrary.Core.Data;
using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Entities;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace MyDigitalLibrary.Core.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly string _jwtSecret;
    public AuthService(IConfiguration config, AppDbContext db)
    {
        _jwtSecret = config["JWT_SECRET"] ?? "dev-secret-change-me-please-change-to-a-secure-random-key-which-is-long";
        _db = db;
    }

    public async Task<(bool Success, string? Error, User? User, string? Token)> Register(string email, string password)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        if (existing != null) return (false, "Email already exists", null, null);

        var hashed = BCrypt.Net.BCrypt.HashPassword(password);
        var entity = new UserEntity { Email = email.ToLowerInvariant(), PasswordHash = hashed, Role = "user" };
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();

        var user = new User { Id = entity.Id, Email = entity.Email, Role = entity.Role };
        var token = GenerateToken(user);
        return (true, null, user, token);
    }

    public async Task<(bool Success, string? Error, User? User, string? Token)> Login(string email, string password)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        if (entity == null) return (false, "User not found", null, null);
        if (!BCrypt.Net.BCrypt.Verify(password, entity.PasswordHash)) return (false, "Invalid credentials", null, null);
        var user = new User { Id = entity.Id, Email = entity.Email, Role = entity.Role };
        var token = GenerateToken(user);
        return (true, null, user, token);
    }

    public User? GetUserById(int userId)
    {
        var entity = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (entity == null) return null;
        return new User { Id = entity.Id, Email = entity.Email, Role = entity.Role };
    }

    public int? ValidateToken(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var raw = System.Text.Encoding.UTF8.GetBytes(_jwtSecret);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var key = sha.ComputeHash(raw);
            var validated = handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true
            }, out var _);
            var userIdClaim = validated.FindFirst("userId")?.Value;
            if (int.TryParse(userIdClaim, out var userId)) return userId;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        // Ensure a 256-bit key for HMAC by hashing the secret if necessary
        var raw = System.Text.Encoding.UTF8.GetBytes(_jwtSecret);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var key = sha.ComputeHash(raw);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] {
                new Claim("userId", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "user")
            }),
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
