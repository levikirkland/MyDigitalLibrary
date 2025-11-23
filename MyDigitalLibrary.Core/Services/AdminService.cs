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
        await _db.SaveChangesAsync();
    }

    public async Task UpdateUserFeaturesAsync(int id, string featuresJson)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.Features = featuresJson;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleUserActiveAsync(int id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) throw new KeyNotFoundException("User not found");
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
    }
}
