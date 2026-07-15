using SFE.Application.Interfaces;
using SFE.Application.Payments;

namespace SFE.Application.Services;

public enum OfflineQrOutcome { Ok, NotFound, NothingDue }

public sealed record OfflineQrResult(
    OfflineQrOutcome Outcome,
    string? Token,
    OfflineDocKind Kind,
    OrderDto? Order);

/// <summary>
/// The one place that turns an orderId into a signed offline token.
/// Used by both SfeApiHost (LAN) and the WPF caisse dialog (in-process).
/// </summary>
public sealed class OfflineQrResolver(
    OfflineQrService qr,
    IInvoiceRepository invoices,
    IPendingOrderProvider pending)
{
    public async Task<OfflineQrResult> ResolveAsync(string orderId, CancellationToken ct)
    {
        // 1) Source of truth: is there a live order waiting to be paid?
        var orders = await pending.GetPendingAsync(ct);
        var order = orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order is null)
            return new(OfflineQrOutcome.NothingDue, null, default, null);

        // 2) Optional enrichment: if the invoice already exists AND is normalized,
        //    we can emit a Fiscal offline token (includes CodeDEFDGI + fiscal QR).
        //    Otherwise emit a Provisional token — the Sunmi still collects payment;
        //    the fiscal receipt is produced later, on the till, after normalization.
        var invoice = await invoices.GetByInvoiceNumberAsync(orderId);

        var isFiscal = invoice is not null
                       && !invoice.IsProforma
                       && invoice.NormalizedAt is not null
                       && !string.IsNullOrWhiteSpace(invoice.CodeDEFDGI);

        var payload = qr.BuildFor(
            orderId: order.OrderId,
            amount: order.Amount,
            currency: order.Currency,
            kind: isFiscal ? OfflineDocKind.Fiscal : OfflineDocKind.Provisional,
            fiscalCode: isFiscal ? invoice!.CodeDEFDGI : null,
            fiscalQr: isFiscal ? invoice!.QRCodeContent : null);

        return new(OfflineQrOutcome.Ok, qr.Encode(payload), payload.Kind, order);
    }
}