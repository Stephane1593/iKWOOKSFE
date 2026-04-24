using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> AuthenticateAsync(string username, string passwordHash);
    Task<List<User>> GetActiveUsersAsync();
    Task<List<User>> GetAllWithRolesAsync();

    // 🆕
    Task<User?> GetWithPosAndRoleAsync(int userId);
    Task<List<User>> GetByPointOfSaleAsync(int posId);
}