using MyDigitalLibrary.Core.Entities;

namespace MyDigitalLibrary.Core.Services;

public interface IFeatureService
{
    Task<IEnumerable<FeatureEntity>> GetFeaturesForUserAsync(int userId);
    Task SetFeatureForUserAsync(int userId, string featureName, bool enabled);
    Task EnsureFeatureExistsAsync(string featureName);
}
