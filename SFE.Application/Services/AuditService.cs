using System.Diagnostics;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAuditWriter _writer;
    private readonly IAuthService _auth;
    private readonly ITimeProvider _time;

    public AuditService(IAuditWriter writer, IAuthService auth, ITimeProvider time)
    {
        _writer = writer;
        _auth = auth;
        _time = time;
    }

    // ── Async (awaitable) ──────────────────────────────

    public async Task LogAsync(
        AuditAction action, AuditModule module, string description,
        string? entityType = null, string? entityId = null,
        string? codeDEFDGI = null, string? invoiceNumber = null,
        string? details = null)
    {
        var entry = Build(action, module, description,
            entityType, entityId, codeDEFDGI, invoiceNumber, details);
        await _writer.WriteAsync(entry);
    }

    // ── Fire-and-forget ────────────────────────────────

    public void Log(
        AuditAction action, AuditModule module, string description,
        string? entityType = null, string? entityId = null,
        string? codeDEFDGI = null, string? invoiceNumber = null,
        string? details = null)
    {
        // IMPORTANT: build the entry synchronously so the timestamp
        // reflects the moment of the action, not the moment the
        // background task happens to run.
        var entry = Build(action, module, description,
            entityType, entityId, codeDEFDGI, invoiceNumber, details);
        _ = SafeWriteAsync(entry);
    }

    // ── Convenience: Invoice ───────────────────────────

    public async Task LogInvoiceAsync(AuditAction action, Invoice invoice)
    {
        var desc = action switch
        {
            AuditAction.InvoiceNormalized
                => $"Facture {invoice.InvoiceNumber} ({invoice.Type}) normalisée — " +
                   $"{invoice.TotalTTC:N2} CDF",
            AuditAction.CreditNoteNormalized
                => $"Facture d'avoir {invoice.InvoiceNumber} ({invoice.CreditNoteNature}) " +
                   $"normalisée — {invoice.TotalTTC:N2} CDF — Réf. orig: " +
                   $"{invoice.OriginalInvoiceReference}",
            AuditAction.AdvanceInvoiceNormalized
                => $"Facture d'acompte {invoice.InvoiceNumber} normalisée — " +
                   $"{invoice.TotalTTC:N2} CDF",
            AuditAction.InvoicePrinted
                => $"Facture {invoice.InvoiceNumber} imprimée",
            AuditAction.InvoiceDuplicated
                => $"Duplicata de {invoice.InvoiceNumber} imprimé",
            _ => $"Facture {invoice.InvoiceNumber} — {action.Label()}"
        };

        var detailJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            invoice.Type,
            invoice.PriceMode,
            invoice.ClientType,
            invoice.ClientName,
            invoice.ClientNIF,
            invoice.TotalHT,
            invoice.TotalTVA,
            invoice.TotalTTC,
            invoice.TotalSpecificTax,
            LinesCount = invoice.Lines.Count,
            PaymentsCount = invoice.Payments.Count,
            invoice.OriginalInvoiceReference,
            invoice.CreditNoteNature,
            invoice.AdvanceGroupId,
            // Preserve the invoice's own fiscal timestamp inside the audit JSON
            // so any later clock tampering is detectable by comparison.
            InvoiceCreatedAtUtc = invoice.CreatedAt.UtcDateTime,
            InvoiceCreatedAtOffset = invoice.CreatedAt.Offset.ToString()
        });

        await LogAsync(action, AuditModule.Invoicing, desc,
            "Invoice", invoice.Id > 0 ? invoice.Id.ToString() : invoice.InvoiceNumber,
            invoice.CodeDEFDGI, invoice.InvoiceNumber, detailJson);
    }

    // ── Convenience: Report ────────────────────────────

    public async Task LogReportAsync(AuditAction action, string reportType,
        int? reportId = null, string? details = null)
    {
        var desc = action switch
        {
            AuditAction.ReportZGenerated => "Rapport Z (clôture de session) généré",
            AuditAction.ReportXGenerated => $"Rapport X ({reportType}) généré",
            AuditAction.ReportAGenerated => "Rapport A (articles) généré",
            AuditAction.ReportExported => $"Rapport {reportType} exporté",
            _ => $"Rapport {reportType}"
        };

        await LogAsync(action, AuditModule.Reports, desc,
            "DailyReport", reportId?.ToString(), details: details);
    }

    // ── Internal ───────────────────────────────────────

    private AuditLogEntry Build(
        AuditAction action, AuditModule module, string description,
        string? entityType, string? entityId,
        string? codeDEFDGI, string? invoiceNumber, string? details)
    {
        var user = _auth.CurrentUser;
        return new AuditLogEntry
        {
            // Unambiguous, DST-proof, anti-fraud timestamp.
            Timestamp = _time.UtcNow,
            Action = action,
            Module = module,
            Description = description,
            UserId = user?.Id,
            UserName = user?.FullName ?? "Système",
            EntityType = entityType ?? "",
            EntityId = entityId ?? "",
            CodeDEFDGI = codeDEFDGI ?? "",
            InvoiceNumber = invoiceNumber ?? "",
            Details = details ?? "",
            PointOfSaleId = user?.PointOfSaleId,
            PointOfSaleName = user?.PointOfSale?.Name ?? ""
        };
    }

    private async Task SafeWriteAsync(AuditLogEntry entry)
    {
        try
        {
            await _writer.WriteAsync(entry);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AuditService] Fire-and-forget write failed: {ex.Message}");
        }
    }
}