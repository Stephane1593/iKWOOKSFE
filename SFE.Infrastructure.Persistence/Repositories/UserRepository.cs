using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _dbSet
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> AuthenticateAsync(string username, string passwordHash)
    {
        return await _dbSet
            .Include(u => u.Role)
            .Include(u => u.PointOfSale)
            .FirstOrDefaultAsync(u => u.Username == username
                                   && u.PasswordHash == passwordHash
                                   && u.IsActive);
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _dbSet
            .Where(u => u.IsActive)
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<List<User>> GetAllWithRolesAsync()
    {
        return await _dbSet
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    // 🆕
    public async Task<User?> GetWithPosAndRoleAsync(int userId)
    {
        return await _dbSet
            .Include(u => u.Role)
            .Include(u => u.PointOfSale)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    // 🆕
    public async Task<List<User>> GetByPointOfSaleAsync(int posId)
    {
        return await _dbSet
            .Include(u => u.Role)
            .Where(u => u.PointOfSaleId == posId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}