using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context) { }

    public async Task<Company?> GetCurrentCompanyAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<Company?> GetWithPointsOfSaleAsync(int companyId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(c => c.PointsOfSale)
            .FirstOrDefaultAsync(c => c.Id == companyId);
    }

    // Inherited GetByIdAsync stays tracking — used by SaveSettingsAsync.
}