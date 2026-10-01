using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SFE.Infrastructure.Persistence;

/// <summary>
/// Writes audit entries to a fresh DbContext, bypassing UnitOfWork's
/// static SemaphoreSlim to prevent deadlocks when logging within a
/// business transaction. SQLite WAL + busy_timeout handles contention.
/// </summary>
public class AuditWriter : IAuditWriter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditWriter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task WriteAsync(AuditLogEntry entry)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Safe fallback for SQLite BIGINT primary keys that don't auto-increment
            if (entry.Id == 0)
            {
                var existingIds = context.Set<AuditLogEntry>().Select(e => (long?)e.Id);
                long maxId = await existingIds.MaxAsync() ?? 0;
                entry.Id = maxId + 1;
            }

            context.Set<AuditLogEntry>().Add(entry);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("audit_errors.log",
                    $"{DateTime.Now}: {ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }

            Debug.WriteLine($"[AuditWriter] Write failed: {ex.Message}");
        }
    }
}