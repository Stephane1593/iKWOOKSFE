using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class ProductCategoryRepository : Repository<ProductCategory>, IProductCategoryRepository
{
    public ProductCategoryRepository(AppDbContext context) : base(context) { }

    public async Task<List<ProductCategory>> GetActiveCategoriesAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ProductCategory?> GetWithProductsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Products.Where(p => p.IsActive))
            .FirstOrDefaultAsync(c => c.Id == id);
    }
}