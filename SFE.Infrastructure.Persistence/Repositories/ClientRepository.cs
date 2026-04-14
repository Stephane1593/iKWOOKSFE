using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class ClientRepository : Repository<Client>, IClientRepository
{
    public ClientRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Client?> GetByNIFAsync(string nif)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.NIF == nif);
    }

    public async Task<List<Client>> SearchAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Where(c => c.Name.ToLower().Contains(term) ||
                        (c.NIF != null && c.NIF.ToLower().Contains(term)) ||
                        (c.Phone != null && c.Phone.Contains(term)))
            .OrderBy(c => c.Name)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Client?> GetWithLoyaltyAccountAsync(int clientId)
    {
        return await _dbSet
            .Include(c => c.LoyaltyAccount)
            .FirstOrDefaultAsync(c => c.Id == clientId);
    }

    public async Task<List<Client>> GetLoyaltyMembersAsync()
    {
        return await _dbSet
            .Where(c => c.IsLoyaltyMember)
            .Include(c => c.LoyaltyAccount)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

}