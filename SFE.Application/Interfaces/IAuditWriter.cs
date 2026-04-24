using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

/// <summary>
/// Low-level writer that bypasses UnitOfWork's write lock.
/// Implemented in Infrastructure.Persistence.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditLogEntry entry);
}