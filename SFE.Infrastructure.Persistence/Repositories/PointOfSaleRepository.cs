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
            .AsNoTracking()                              // ← fresh read every call
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    public async Task<PointOfSale?> GetActiveByCodeAsync(string code)
    {
        // Used by the sales flow to identify the active POS for the session.
        // If you never mutate the returned entity, keep AsNoTracking().
        // If some caller does `pos.Foo = ...; await UoW.SaveChangesAsync();`
        // then REMOVE AsNoTracking() from this one.
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive);
    }

    public async Task<List<PointOfSale>> GetActiveAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    public async Task<List<PointOfSale>> GetByCompanyWithOperatorsAsync(int companyId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Operators.Where(u => u.IsActive))
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Code)
            .ToListAsync();
    }

    public async Task<PointOfSale?> GetWithOperatorsAsync(int posId)
    {
        // Read-mostly (dialog display). If the editor mutates this graph
        // and saves it back, remove AsNoTracking() here.
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Operators)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(p => p.Id == posId);
    }

    // NOTE: The inherited GetByIdAsync from Repository<PointOfSale> is what
    // SaveSettingsAsync uses to update. It must remain TRACKING (no AsNoTracking).
}