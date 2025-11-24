using Microsoft.EntityFrameworkCore;
using MyDigitalLibrary.Core.Data;
using MyDigitalLibrary.Core.Entities;
using System.Text.Json;

namespace MyDigitalLibrary.Core.Services;

public class FeatureService : IFeatureService
{
    private readonly AppDbContext _db;
    public FeatureService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<FeatureEntity>> GetFeaturesForUserAsync(int userId)
    {
        // Prefer features table; fallback to users.Features JSON when table missing
        try
        {
            return await _db.Features.Where(f => f.UserId == userId).OrderBy(f => f.Name).ToListAsync();
        }
        catch (Exception ex) when (ex.Message?.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Features)) return Enumerable.Empty<FeatureEntity>();
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.Features) ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                return dict.Select(kv => new FeatureEntity { UserId = userId, Name = kv.Key, Enabled = kv.Value }).ToList();
            }
            catch
            {
                return Enumerable.Empty<FeatureEntity>();
            }
        }
    }

    public async Task SetFeatureForUserAsync(int userId, string featureName, bool enabled)
    {
        try
        {
            var existing = await _db.Features.FirstOrDefaultAsync(f => f.UserId == userId && f.Name == featureName);
            if (existing == null)
            {
                var f = new FeatureEntity { UserId = userId, Name = featureName, Enabled = enabled };
                _db.Features.Add(f);
            }
            else
            {
                existing.Enabled = enabled;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex.Message?.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) throw new KeyNotFoundException("User not found");
            Dictionary<string, bool> dict;
            if (string.IsNullOrWhiteSpace(user.Features)) dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            else
            {
                try { dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.Features) ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); }
                catch { dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); }
            }
            dict[featureName] = enabled;
            user.Features = JsonSerializer.Serialize(dict);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task EnsureFeatureExistsAsync(string featureName)
    {
        try
        {
            var global = await _db.Features.FirstOrDefaultAsync(f => f.UserId == 0 && f.Name == featureName);
            if (global == null)
            {
                _db.Features.Add(new FeatureEntity { UserId = 0, Name = featureName, Enabled = false });
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex) when (ex.Message?.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // No-op when features table missing
            return;
        }
    }
}
