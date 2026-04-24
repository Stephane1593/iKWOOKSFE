using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class PointOfSaleRepository : Repository<PointOfSale>, IPointOfSaleRepository
{
    public PointOfSaleRepository(AppDbContext context) : base(context) { }

    public async Task<List<PointOfSale>> GetByCompanyIdAsync(int companyId)
    {
        return await _dbSet
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    public async Task<PointOfSale?> GetActiveByCodeAsync(string code)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive);
    }

    public async Task<List<PointOfSale>> GetActiveAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    // 🆕
    public async Task<List<PointOfSale>> GetByCompanyWithOperatorsAsync(int companyId)
    {
        return await _dbSet
            .Include(p => p.Operators.Where(u => u.IsActive))
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    // 🆕
    public async Task<PointOfSale?> GetWithOperatorsAsync(int posId)
    {
        return await _dbSet
            .Include(p => p.Operators)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(p => p.Id == posId);
    }
}