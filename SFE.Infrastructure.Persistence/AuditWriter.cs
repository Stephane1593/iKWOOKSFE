using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using System.Diagnostics;

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
            context.Set<AuditLogEntry>().Add(entry);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit must NEVER crash the application
            Debug.WriteLine($"[AuditWriter] Write failed: {ex.Message}");
        }
    }
}