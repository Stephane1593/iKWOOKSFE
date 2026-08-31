using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Interfaces;

public interface IAuditService
{
    /// <summary>Async write — blocks until persisted.</summary>
    Task LogAsync(
        AuditAction action,
        AuditModule module,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? codeDEFDGI = null,
        string? invoiceNumber = null,
        string? details = null);

    /// <summary>Fire-and-forget — never throws.</summary>
    void Log(
        AuditAction action,
        AuditModule module,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? codeDEFDGI = null,
        string? invoiceNumber = null,
        string? details = null);

    /// <summary>Convenience — extracts all invoice fields automatically.</summary>
    Task LogInvoiceAsync(AuditAction action, Invoice invoice);

    /// <summary>Convenience for report generation events.</summary>
    Task LogReportAsync(AuditAction action, string reportType,
        int? reportId = null, string? details = null);
}