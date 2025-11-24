using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDigitalLibrary.Core.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    public AdminService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<UserEntity>> GetAllUsersAsync()
    {
        return await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<UserEntity?> GetUserAsync(int id) => await _db.Users.FindAsync(id);

    public async Task UpdateUserRoleAsync(int id, string role)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateUserFeaturesAsync(int id, string featuresJson)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.Features = featuresJson;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleUserActiveAsync(int id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateUserDisplayNameAsync(int id, string? displayName)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.DisplayName = displayName;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
