using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

/// <summary>
/// Journal électronique (DGI §19) — traces toutes les factures,
/// rapports et actions utilisateurs avec le Code DEF/DGI.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // ── What ──
    public AuditAction Action { get; set; }
    public AuditModule Module { get; set; }
    public string Description { get; set; } = "";

    // ── Who ──
    public int? UserId { get; set; }
    public string UserName { get; set; } = "";

    // ── Entity reference ──
    public string EntityType { get; set; } = "";   // "Invoice", "Product", …
    public string EntityId { get; set; } = "";      // PK or reference

    // ── DGI-specific (§19) ──
    public string CodeDEFDGI { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";

    // ── Context ──
    public string Details { get; set; } = "";       // JSON extra data
    public int? PointOfSaleId { get; set; }
    public string PointOfSaleName { get; set; } = "";
}