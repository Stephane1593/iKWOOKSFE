// File: SFE.Infrastructure/Persistence/Repositories/StockMovementRepository.cs
using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public class StockMovementRepository : Repository<StockMovement>, IStockMovementRepository
{
    private readonly AppDbContext _db;

    public StockMovementRepository(AppDbContext context) : base(context)
    {
        _db = context;
    }

    public async Task<List<StockMovement>> GetByProductAndPosAsync(
        int productId, int pointOfSaleId, int maxResults = 50)
    {
        return await _dbSet
            .Include(m => m.Product)
            .Include(m => m.PointOfSale)
            .Where(m => m.ProductId == productId && m.PointOfSaleId == pointOfSaleId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<List<StockMovement>> GetByPosAsync(
        int pointOfSaleId, DateTime? from = null, DateTime? to = null)
    {
        var query = _dbSet
            .Include(m => m.Product)
            .Where(m => m.PointOfSaleId == pointOfSaleId);

        if (from.HasValue)
            query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.CreatedAt < to.Value.Date.AddDays(1));

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StockMovement>> GetByReferenceAsync(string reference)
    {
        return await _dbSet
            .Include(m => m.Product)
            .Include(m => m.PointOfSale)
            .Where(m => m.Reference == reference)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StockMovement>> GetByTypeAsync(
        StockMovementType type, int pointOfSaleId,
        DateTime? from = null, DateTime? to = null)
    {
        var query = _dbSet
            .Include(m => m.Product)
            .Where(m => m.Type == type && m.PointOfSaleId == pointOfSaleId);

        if (from.HasValue) query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAt < to.Value.Date.AddDays(1));

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<List<StockMovement>> GetByTransferReferenceAsync(string transferReference)
    {
        return await _dbSet
            .Include(m => m.Product)
            .Include(m => m.PointOfSale)
            .Where(m => m.TransferReference == transferReference)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<StockMovement> Items, int TotalCount)> SearchAsync(
        StockMovementSearchCriteria criteria, int page, int pageSize)
    {
        var query = _db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.PointOfSale)
            .AsQueryable();

        if (criteria.PointOfSaleId.HasValue)
            query = query.Where(m => m.PointOfSaleId == criteria.PointOfSaleId.Value);

        if (criteria.ProductId.HasValue)
            query = query.Where(m => m.ProductId == criteria.ProductId.Value);

        if (criteria.Type.HasValue)
            query = query.Where(m => m.Type == criteria.Type.Value);

        if (criteria.DateFrom.HasValue)
            query = query.Where(m => m.CreatedAt >= criteria.DateFrom.Value);

        if (criteria.DateTo.HasValue)
            query = query.Where(m => m.CreatedAt < criteria.DateTo.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var s = criteria.SearchText.Trim().ToLower();
            query = query.Where(m =>
                m.Product.Name.ToLower().Contains(s) ||
                m.Product.Code.ToLower().Contains(s) ||
                m.Reference.ToLower().Contains(s) ||
                m.Notes.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(criteria.OperatorName))
            query = query.Where(m => m.OperatorName == criteria.OperatorName);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}