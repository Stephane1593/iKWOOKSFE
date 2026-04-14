using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class PosStockRepository : Repository<PosStock>, IPosStockRepository
{
    private readonly AppDbContext _db;

    public PosStockRepository(AppDbContext context) : base(context)
    {
        _db = context;
    }

    public async Task<PosStock?> GetByProductAndPosAsync(int productId, int pointOfSaleId)
    {
        return await _dbSet
            .Include(ps => ps.Product)
            .Include(ps => ps.PointOfSale)
            .FirstOrDefaultAsync(ps => ps.ProductId == productId
                                    && ps.PointOfSaleId == pointOfSaleId);
    }

    public async Task<List<PosStock>> GetByPosAsync(int pointOfSaleId)
    {
        return await _dbSet
            .Include(ps => ps.Product)
                .ThenInclude(p => p.Category)
            .Where(ps => ps.PointOfSaleId == pointOfSaleId && ps.Product.IsActive)
            .OrderBy(ps => ps.Product.Name)
            .ToListAsync();
    }

    public async Task<List<PosStock>> GetByProductAsync(int productId)
    {
        return await _dbSet
            .Include(ps => ps.PointOfSale)
            .Where(ps => ps.ProductId == productId && ps.PointOfSale.IsActive)
            .OrderBy(ps => ps.PointOfSale.Code)
            .ToListAsync();
    }

    public async Task<List<PosStock>> GetLowStockByPosAsync(int pointOfSaleId)
    {
        return await _dbSet
            .Include(ps => ps.Product)
                .ThenInclude(p => p.Category)
            .Where(ps => ps.PointOfSaleId == pointOfSaleId
                      && ps.Product.IsActive
                      && ps.Product.TrackStock
                      && ps.Quantity <= (ps.MinStockLevel ?? ps.Product.MinStockLevel))
            .OrderBy(ps => ps.Quantity)
            .ToListAsync();
    }

    public async Task<List<PosStock>> GetOutOfStockByPosAsync(int pointOfSaleId)
    {
        return await _dbSet
            .Include(ps => ps.Product)
                .ThenInclude(p => p.Category)
            .Where(ps => ps.PointOfSaleId == pointOfSaleId
                      && ps.Product.IsActive
                      && ps.Product.TrackStock
                      && ps.Quantity <= 0)
            .OrderBy(ps => ps.Product.Name)
            .ToListAsync();
    }

    public async Task<List<PosStock>> GetAllLowStockAsync()
    {
        return await _dbSet
            .Include(ps => ps.Product)
                .ThenInclude(p => p.Category)
            .Include(ps => ps.PointOfSale)
            .Where(ps => ps.Product.IsActive
                      && ps.Product.TrackStock
                      && ps.PointOfSale.IsActive
                      && ps.Quantity <= (ps.MinStockLevel ?? ps.Product.MinStockLevel))
            .OrderBy(ps => ps.Quantity)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalStockAsync(int productId)
    {
        var quantities = await _dbSet
            .Where(ps => ps.ProductId == productId && ps.PointOfSale.IsActive)
            .Select(ps => ps.Quantity)
            .ToListAsync();                 // ← matérialise en mémoire

        return quantities.Sum();            // ← Sum() exécuté en C#, pas en SQL
    }

    public async Task<List<PosStock>> SearchInPosAsync(int pointOfSaleId, string query, int maxResults = 30)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<PosStock>();

        var q = query.ToLower().Trim();

        return await _dbSet
            .Include(ps => ps.Product)
                .ThenInclude(p => p.Category)
            .Where(ps => ps.PointOfSaleId == pointOfSaleId
                      && ps.Product.IsActive
                      && (ps.Product.Name.ToLower().Contains(q)
                          || ps.Product.Code.ToLower().Contains(q)
                          || ps.Product.Barcode.Contains(q)))
            .OrderBy(ps => ps.Product.Name)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<int> GetLowStockCountAsync(int pointOfSaleId)
    {
        return await _dbSet
            .Where(ps => ps.PointOfSaleId == pointOfSaleId
                      && ps.Product.IsActive
                      && ps.Product.TrackStock
                      && ps.Quantity <= (ps.MinStockLevel ?? ps.Product.MinStockLevel))
            .CountAsync();
    }

    public async Task<int> GetTotalLowStockCountAsync()
    {
        return await _dbSet
            .Where(ps => ps.Product.IsActive
                      && ps.Product.TrackStock
                      && ps.PointOfSale.IsActive
                      && ps.Quantity <= (ps.MinStockLevel ?? ps.Product.MinStockLevel))
            .CountAsync();
    }
}