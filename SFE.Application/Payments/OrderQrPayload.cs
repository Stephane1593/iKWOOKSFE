namespace SFE.Application.Payments;

public enum OfflineDocKind
{
    /// <summary>Already normalized: Sunmi may print a full fiscal receipt offline.</summary>
    Fiscal = 0,
    /// <summary>Proforma / not yet fiscal: Sunmi prints a PROVISIONAL ack only.</summary>
    Provisional = 1
}

/// <summary>
/// Encoded into the offline QR as "payload.signature".
/// Kept deliberately compact — every field costs QR modules.
/// </summary>
public record OrderQrPayload(
    int Version,
    string IdempotencyKey,   // minted by SFE — anti-double-charge anchor
    string OrderId,          // == InvoiceNumber
    decimal Amount,
    string Currency,
    string CaisseId,
    long IssuedUnix,
    OfflineDocKind Kind,
    string? FiscalCode,      // Invoice.CodeDEFDGI  — only when Kind == Fiscal
    string? FiscalQr);       // Invoice.QRCodeContent — only when Kind == Fiscal