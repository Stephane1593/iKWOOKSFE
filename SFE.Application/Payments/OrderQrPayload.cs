using SFE.Application.Services;

namespace SFE.Application.Payments;

public enum OfflineDocKind
{
    /// <summary>Already normalized: Sunmi may print a full fiscal receipt offline.</summary>
    Fiscal = 0,

    /// <summary>Proforma / not yet fiscal: Sunmi prints a provisional receipt.</summary>
    Provisional = 1
}

/// <summary>
/// Encoded into the offline QR as "payload.signature".
/// This now carries the same ReceiptDocument JSON structure used by
/// /receipts/json/proforma/{id} and /receipts/json/fiscal/{id}.
/// </summary>
public record OrderQrPayload(
    int Version,
    string IdempotencyKey,
    string OrderId,
    decimal Amount,
    string Currency,
    string CaisseId,
    long IssuedUnix,
    OfflineDocKind Kind,
    ReceiptDocument[] Documents,
    string? FiscalCode,
    string? FiscalQr);