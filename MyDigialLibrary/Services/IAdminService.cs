using MyDigitalLibrary.Entities;

namespace MyDigitalLibrary.Services;

public interface IAdminService
{
    Task<IEnumerable<UserEntity>> GetAllUsersAsync();
    Task<UserEntity?> GetUserAsync(int id);
    Task UpdateUserRoleAsync(int id, string role);
    Task UpdateUserFeaturesAsync(int id, string featuresJson);
    Task ToggleUserActiveAsync(int id, bool isActive);
}
