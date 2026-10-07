using SyteQuery.Features.Database.Data;
using SyteQuery.Features.Database.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace SyteQuery.Features.Environments.Repositories;

public class UserEnvironmentRepository : IUserEnvironmentRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;

    public UserEnvironmentRepository(ApplicationDbContext context, IDataProtectionProvider provider)
    {
        _context = context;
        // NOTE: intentionally kept as "CsiQueryTool.*" (not renamed) even though the project
        // was renamed to SyteQuery. This purpose string seeds the Data Protection key derived
        // for encrypting/decrypting stored SyteLine environment passwords; changing it would
        // break decryption of every already-stored password for the live production app.
        _protector = provider.CreateProtector("CsiQueryTool.UserEnvironment.Password");
    }

    public async Task<List<UserEnvironmentDb>> GetByUserIdAsync(string userId)
    {
        return await _context.UserEnvironments
            .Where(e => e.UserId == userId && e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<UserEnvironmentDb?> GetByIdAsync(string userId, Guid environmentId)
    {
        return await _context.UserEnvironments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.EnvironmentId == environmentId && e.IsActive);
    }

    public async Task<UserEnvironmentDb> AddAsync(UserEnvironmentDb environment)
    {
        _context.UserEnvironments.Add(environment);
        await _context.SaveChangesAsync();
        return environment;
    }

    public async Task UpdateAsync(UserEnvironmentDb environment)
    {
        environment.ModifiedAt = DateTime.UtcNow;
        _context.UserEnvironments.Update(environment);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string userId, Guid environmentId)
    {
        var environment = await GetByIdAsync(userId, environmentId);
        if (environment != null)
        {
            environment.IsActive = false;
            environment.ModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetCountByUserIdAsync(string userId)
    {
        return await _context.UserEnvironments
            .CountAsync(e => e.UserId == userId && e.IsActive);
    }

    public string EncryptPassword(string password)
    {
        return _protector.Protect(password);
    }

    public string DecryptPassword(string encryptedPassword)
    {
        return _protector.Unprotect(encryptedPassword);
    }
}
