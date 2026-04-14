using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Product?> GetByCodeAsync(string code)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Code == code);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Barcode == barcode);
    }

    public async Task<List<Product>> GetActiveAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> SearchAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Where(p => p.IsActive &&
                (p.Name.ToLower().Contains(term) ||
                 p.Code.ToLower().Contains(term) ||
                 (p.Barcode != null && p.Barcode.Contains(term))))
            .OrderBy(p => p.Name)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<Product>> GetByTaxGroupAsync(TaxGroup taxGroup)
    {
        return await _dbSet
            .Where(p => p.TaxGroup == taxGroup && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> GetActiveProductsAsync()
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> SearchAsync(string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Product>();

        var q = query.ToLower().Trim();

        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.IsActive &&
                (p.Name.ToLower().Contains(q) ||
                 p.Code.ToLower().Contains(q) ||
                 p.Barcode.Contains(q)))
            .OrderBy(p => p.Name)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<List<Product>> GetByCategoryAsync(int categoryId)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> GetFavoritesAsync()
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.IsFavorite && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> GetLowStockAsync()
    {
        return await _dbSet
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.TrackStock && p.StockQuantity <= p.MinStockLevel)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();
    }

    public async Task<int> GetActiveCountAsync()
    {
        return await _dbSet.CountAsync(p => p.IsActive);
    }
}