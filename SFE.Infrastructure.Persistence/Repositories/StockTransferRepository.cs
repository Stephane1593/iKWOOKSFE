// File: SFE.Infrastructure/Persistence/Repositories/StockTransferRepository.cs
using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public class StockTransferRepository : Repository<StockTransfer>, IStockTransferRepository
{
    private readonly AppDbContext _db;

    public StockTransferRepository(AppDbContext context) : base(context)
    {
        _db = context;
    }

    public async Task<StockTransfer?> GetWithLinesAsync(int transferId)
    {
        return await _dbSet
            .Include(t => t.FromPointOfSale)
            .Include(t => t.ToPointOfSale)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(t => t.Id == transferId);
    }

    public async Task<List<StockTransfer>> GetByStatusAsync(TransferStatus status)
    {
        return await _dbSet
            .Include(t => t.FromPointOfSale)
            .Include(t => t.ToPointOfSale)
            .Include(t => t.Lines)
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StockTransfer>> GetByPosAsync(int pointOfSaleId, bool asSender = true)
    {
        var query = asSender
            ? _dbSet.Where(t => t.FromPointOfSaleId == pointOfSaleId)
            : _dbSet.Where(t => t.ToPointOfSaleId == pointOfSaleId);

        return await query
            .Include(t => t.FromPointOfSale)
            .Include(t => t.ToPointOfSale)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StockTransfer>> GetPendingForPosAsync(int pointOfSaleId)
    {
        return await _dbSet
            .Include(t => t.FromPointOfSale)
            .Include(t => t.ToPointOfSale)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Product)
            .Where(t => t.ToPointOfSaleId == pointOfSaleId
                     && (t.Status == TransferStatus.Pending || t.Status == TransferStatus.InTransit))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<string> GenerateNextNumberAsync(int year)
    {
        var pattern = $"TRF-{year}/";
        var last = await _dbSet
            .Where(t => t.TransferNumber.StartsWith(pattern))
            .OrderByDescending(t => t.TransferNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null)
        {
            var parts = last.TransferNumber.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[1], out var n))
                next = n + 1;
        }

        return $"TRF-{year}/{next:D4}";
    }
}