using SyteQuery.Features.Database.Data;
using Microsoft.EntityFrameworkCore;

namespace SyteQuery.Features.Common.Services;

/// <summary>
/// Query display preferences tied to the (single, local) user profile.
/// Split out from what used to be SubscriptionService - the preference storage had
/// nothing to do with subscriptions, it just lived there because UserProfile did.
/// </summary>
public interface IUserPreferencesService
{
    Task<bool> GetShowAllDataByDefaultAsync(string userId);
    Task<bool> SetShowAllDataByDefaultAsync(string userId, bool showAllData);
}

public class UserPreferencesService : IUserPreferencesService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserPreferencesService> _logger;

    public UserPreferencesService(ApplicationDbContext context, ILogger<UserPreferencesService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> GetShowAllDataByDefaultAsync(string userId)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
        return profile?.ShowAllDataByDefault ?? false;
    }

    public async Task<bool> SetShowAllDataByDefaultAsync(string userId, bool showAllData)
    {
        try
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
            if (profile == null)
            {
                _logger.LogWarning("Cannot update preference - user profile not found for user {UserId}", userId);
                return false;
            }

            profile.ShowAllDataByDefault = showAllData;
            profile.ModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated ShowAllDataByDefault preference to {Value} for user {UserId}",
                showAllData, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preference for user {UserId}", userId);
            return false;
        }
    }
}
