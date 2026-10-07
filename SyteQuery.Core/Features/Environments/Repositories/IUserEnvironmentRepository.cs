using SyteQuery.Features.Database.Entities;

namespace SyteQuery.Features.Environments.Repositories;

public interface IUserEnvironmentRepository
{
    Task<List<UserEnvironmentDb>> GetByUserIdAsync(string userId);
    Task<UserEnvironmentDb?> GetByIdAsync(string userId, Guid environmentId);
    Task<UserEnvironmentDb> AddAsync(UserEnvironmentDb environment);
    Task UpdateAsync(UserEnvironmentDb environment);
    Task DeleteAsync(string userId, Guid environmentId);
    Task<int> GetCountByUserIdAsync(string userId);
    string EncryptPassword(string password);
    string DecryptPassword(string encryptedPassword);
}
