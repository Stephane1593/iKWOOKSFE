using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : Repository<AuditLogEntry>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }

    public async Task<(List<AuditLogEntry> Items, int TotalCount)> SearchAsync(
        AuditLogSearchCriteria criteria, int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        // ── Date range (DateTimeOffset end-to-end) ──
        // Caller is responsible for passing already-normalized bounds
        // (start-of-day / end-of-day in the user's local offset).
        if (criteria.DateFrom.HasValue)
        {
            var from = criteria.DateFrom.Value;
            query = query.Where(e => e.Timestamp >= from);
        }
        if (criteria.DateTo.HasValue)
        {
            var to = criteria.DateTo.Value;
            query = query.Where(e => e.Timestamp <= to);
        }

        // ── Module ──
        if (criteria.Module.HasValue)
            query = query.Where(e => e.Module == criteria.Module.Value);

        // ── Action ──
        if (criteria.Action.HasValue)
            query = query.Where(e => e.Action == criteria.Action.Value);

        // ── User ──
        if (!string.IsNullOrWhiteSpace(criteria.UserName))
            query = query.Where(e => e.UserName.Contains(criteria.UserName));

        // ── Free text search ──
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var txt = criteria.SearchText.Trim().ToLower();
            query = query.Where(e =>
                e.Description.ToLower().Contains(txt) ||
                e.InvoiceNumber.ToLower().Contains(txt) ||
                e.CodeDEFDGI.ToLower().Contains(txt) ||
                e.EntityId.ToLower().Contains(txt) ||
                e.UserName.ToLower().Contains(txt));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<AuditLogStats> GetStatsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        // Bounds are passed in already normalized by the caller
        // (ToStartOfDayOffset / ToEndOfDayOffset in the ViewModel).
        var entries = await _dbSet.AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .GroupBy(e => e.Module)
            .Select(g => new { Module = g.Key, Count = g.Count() })
            .ToListAsync();

        return new AuditLogStats
        {
            TotalCount = entries.Sum(e => e.Count),

            InvoiceCount = entries.Where(e => e.Module == AuditModule.Invoicing)
                                  .Sum(e => e.Count),

            ReportCount = entries.Where(e => e.Module == AuditModule.Reports)
                                  .Sum(e => e.Count),

            AuthCount = entries.Where(e => e.Module == AuditModule.Authentication
                                           || e.Module == AuditModule.Session)
                                  .Sum(e => e.Count),

            StockCount = entries.Where(e => e.Module == AuditModule.Stock)
                                  .Sum(e => e.Count),

            SettingsCount = entries.Where(e => e.Module == AuditModule.Settings
                                            || e.Module == AuditModule.Users)
                                   .Sum(e => e.Count),

            OtherCount = entries.Where(e => e.Module == AuditModule.Products
                                            || e.Module == AuditModule.Clients
                                            || e.Module == AuditModule.System)
                                   .Sum(e => e.Count),
        };
    }

    public async Task<List<string>> GetDistinctUserNamesAsync()
    {
        return await _dbSet.AsNoTracking()
            .Where(e => e.UserName != "")
            .Select(e => e.UserName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }
}