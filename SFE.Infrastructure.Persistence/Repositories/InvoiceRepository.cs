using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    private readonly AppDbContext _db;
    private readonly ITimeProvider _time;
    public InvoiceRepository(AppDbContext context, ITimeProvider time) : base(context) {
        _db = context;
        _time = time;
    }

    public async Task<Invoice?> GetWithDetailsAsync(int invoiceId)
    {
        return await _dbSet
            .Include(i => i.Lines.OrderBy(l => l.LineNumber))
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _dbSet.FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<Invoice?> GetByCodeDEFDGIAsync(string codeDEFDGI)
    {
        return await _dbSet
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.CodeDEFDGI == codeDEFDGI);
    }

    public async Task<List<Invoice>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        var fromOff = new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Utc));
        var toOff = new DateTimeOffset(DateTime.SpecifyKind(to, DateTimeKind.Utc));

        return await _dbSet
            .Where(i => i.CreatedAt >= fromOff && i.CreatedAt <= toOff
                     && i.Status == InvoiceStatus.Normalized)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByTypeAsync(InvoiceType type)
    {
        return await _dbSet
            .Where(i => i.Type == type && i.Status == InvoiceStatus.Normalized)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<string> GenerateNextInvoiceNumberAsync(
        InvoiceType type, int year, int pointOfSaleId)
    {
        // Récupère le code du POS
        var posCode = await _db.PointsOfSale
            .Where(p => p.Id == pointOfSaleId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync()
            ?? $"POS{pointOfSaleId:D2}";

        // Format final : "POS01-FV-2026/0001"
        var prefix = $"{posCode}-{type}-{year}/";

        // Récupère tous les numéros pour CE pos × type × année
        var existingNumbers = await _dbSet
            .Where(i => i.PointOfSaleId == pointOfSaleId
                     && i.Type == type
                     && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        int maxNum = 0;
        foreach (var num in existingNumbers)
        {
            var idx = num.LastIndexOf('/');
            if (idx > 0 && int.TryParse(num.AsSpan(idx + 1), out var n))
                maxNum = Math.Max(maxNum, n);
        }

        return $"{prefix}{maxNum + 1:D4}";
    }

    public async Task<int> GetTodayCountAsync()
    {
        var todayStart = new DateTimeOffset(_time.UtcToday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return await _dbSet.CountAsync(i =>
            i.CreatedAt >= todayStart && i.Status == InvoiceStatus.Normalized);
    }

    public async Task<decimal> GetTodayTotalAsync()
    {
        var todayStart = new DateTimeOffset(_time.UtcToday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var totals = await _dbSet
            .Where(i => i.CreatedAt >= todayStart && i.Status == InvoiceStatus.Normalized)
            .Select(i => i.TotalTTC)
            .ToListAsync();
        return totals.Sum();
    }

    public async Task<List<Invoice>> GetCreditNotesForOriginalAsync(string originalCodeDEFDGI)
    {
        return await _dbSet
            .Include(i => i.Lines)
            .Where(i => i.OriginalInvoiceReference == originalCodeDEFDGI
                     && i.Status == InvoiceStatus.Normalized
                     && (i.Type == InvoiceType.FA || i.Type == InvoiceType.EA))
            .ToListAsync();
    }

    public async Task<string?> GetLastNumberAsync(InvoiceType type)
    {
        return await _db.Invoices
            .Where(i => i.Type == type)
            .OrderByDescending(i => i.Id)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<Invoice> Items, int TotalCount)> SearchAsync(
     InvoiceSearchCriteria criteria, int page, int pageSize)
    {
        var query = _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .AsQueryable();

        // ── Filtres ──
        if (criteria.DateFrom.HasValue)
        {
            var fromOff = new DateTimeOffset(DateTime.SpecifyKind(criteria.DateFrom.Value, DateTimeKind.Utc));
            query = query.Where(i => i.CreatedAt >= fromOff);
        }

        if (criteria.DateTo.HasValue)
        {
            var endOff = new DateTimeOffset(
                DateTime.SpecifyKind(criteria.DateTo.Value.Date.AddDays(1), DateTimeKind.Utc));
            query = query.Where(i => i.CreatedAt < endOff);
        }

        if (criteria.Type.HasValue)
            query = query.Where(i => i.Type == criteria.Type.Value);

        if (criteria.Status.HasValue)
            query = query.Where(i => i.Status == criteria.Status.Value);

        if (criteria.PaymentType.HasValue)
            query = query.Where(i => i.Payments.Any(p => p.PaymentType == criteria.PaymentType.Value));

        if (!string.IsNullOrWhiteSpace(criteria.OperatorName))
            query = query.Where(i => i.OperatorName == criteria.OperatorName);

        if (criteria.MinAmount.HasValue)
            query = query.Where(i => i.TotalTTC >= criteria.MinAmount.Value);

        if (criteria.MaxAmount.HasValue)
            query = query.Where(i => i.TotalTTC <= criteria.MaxAmount.Value);

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var search = criteria.SearchText.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(search) ||
                (i.CodeDEFDGI != null && i.CodeDEFDGI.ToLower().Contains(search)) ||
                (i.ClientName != null && i.ClientName.ToLower().Contains(search)) ||
                (i.ClientNIF != null && i.ClientNIF.ToLower().Contains(search)));
        }

        // ── Comptage total ──
        var totalCount = await query.CountAsync();

        // ── Pagination triée par date décroissante ──
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<InvoicePeriodStats> GetPeriodStatsAsync(DateTime from, DateTime to)
    {
        var fromOff = new DateTimeOffset(DateTime.SpecifyKind(from, DateTimeKind.Utc));
        var endOff = new DateTimeOffset(DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc));

        var invoices = await _db.Invoices
            .Where(i => i.CreatedAt >= fromOff && i.CreatedAt < endOff
                     && i.Status == InvoiceStatus.Normalized)
            .ToListAsync();

        if (invoices.Count == 0)
        {
            return new InvoicePeriodStats();
        }

        return new InvoicePeriodStats
        {
            TotalCount = invoices.Count,
            TotalHT = invoices.Sum(i => i.TotalHT),
            TotalTVA = invoices.Sum(i => i.TotalTVA),
            TotalTTC = invoices.Sum(i => i.TotalTTC),
            FVCount = invoices.Count(i => i.Type == InvoiceType.FV),
            FTCount = invoices.Count(i => i.Type == InvoiceType.FT),
            EVCount = invoices.Count(i => i.Type == InvoiceType.EV),
            ETCount = invoices.Count(i => i.Type == InvoiceType.ET),
            EACount = invoices.Count(i => i.Type == InvoiceType.EA),
            FACount = invoices.Count(i => i.Type == InvoiceType.FA),
            AverageAmount = Math.Round(invoices.Average(i => i.TotalTTC), 0),
            MaxInvoiceAmount = invoices.Max(i => i.TotalTTC)
        };
    }

    public async Task<Invoice?> GetByCodeDEFAsync(string codeDEF)
    {
        return await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.CodeDEFDGI == codeDEF);
    }

    public async Task<List<string>> GetDistinctOperatorNamesAsync()
    {
        return await _context.Invoices
            .Where(i => i.OperatorName != null && i.OperatorName != "")
            .Select(i => i.OperatorName!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task<Invoice?> GetByNumberAsync(string invoiceNumber)
    {
        return await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<List<Invoice>> GetRecentAsync(int count)
    {
        return await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    // Add to InvoiceRepository:

    public async Task<List<Invoice>> GetAdvancesByGroupAsync(string advanceGroupId)
    {
        return await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Where(i => i.AdvanceGroupId == advanceGroupId
                         && (i.Type == InvoiceType.FT || i.Type == InvoiceType.ET)
                         && i.Status == InvoiceStatus.Normalized)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetByAdvanceGroupAsync(string advanceGroupId)
    {
        return await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Where(i => i.AdvanceGroupId == advanceGroupId
                         && i.Status == InvoiceStatus.Normalized)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> CodeDEFDGIExistsAsync(string codeDEFDGI)
    {
        return await _db.Invoices
            .AnyAsync(i => i.CodeDEFDGI == codeDEFDGI && i.Status == InvoiceStatus.Normalized);
    }

    public async Task<string> GenerateNextProformaNumberAsync(int year, int pointOfSaleId)
    {
        var posCode = await _db.PointsOfSale
            .Where(p => p.Id == pointOfSaleId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync()
            ?? $"POS{pointOfSaleId:D2}";

        var prefix = $"PR-{posCode}-{year}/";

        var existing = await _dbSet
            .Where(i => i.PointOfSaleId == pointOfSaleId
                     && i.Type == InvoiceType.PRO
                     && i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        int max = 0;
        foreach (var n in existing)
        {
            var idx = n.LastIndexOf('/');
            if (idx > 0 && int.TryParse(n.AsSpan(idx + 1), out var v))
                max = Math.Max(max, v);
        }

        return $"{prefix}{max + 1:D4}";
    }

    public async Task<List<Invoice>> GetActiveProformasAsync(
        int? pointOfSaleId = null, bool excludeExpired = true)
    {
        var now = _time.UtcNow.UtcDateTime;
        var q = _dbSet
            .Include(i => i.Lines)
            .Where(i => i.Type == InvoiceType.PRO
                     && i.ConvertedToInvoiceId == null);

        if (pointOfSaleId.HasValue)
            q = q.Where(i => i.PointOfSaleId == pointOfSaleId.Value);

        if (excludeExpired)
            q = q.Where(i => i.ProformaValidUntil == null
                          || i.ProformaValidUntil > now);

        return await q.OrderByDescending(i => i.CreatedAt).ToListAsync();
    }
}