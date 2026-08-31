using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Domain.Abstractions;

namespace SFE.Application.Services;

public enum OfflineQrOutcome
{
    Ok,
    NotFound,
    NothingDue,
    Error
}

public sealed record OfflineQrResult(
    OfflineQrOutcome Outcome,
    string Token,
    OfflineDocKind Kind,
    OrderDto? Order);

/// <summary>
/// The one place that turns an orderId into a signed offline token.
/// Used by both SfeApiHost LAN endpoint and the WPF caisse dialog.
/// </summary>
public sealed class OfflineQrResolver(
    OfflineQrService qr,
    IInvoiceRepository invoices,
    IPendingOrderProvider pending,
    InMemoryPendingOrderStore store,
    IUnitOfWork uow,
    ITimeProvider time)
{
    private const int SunmiPaperWidthMm = 58;

    public async Task<OfflineQrResult> ResolveAsync(string orderId, CancellationToken ct)
    {
        orderId = Uri.UnescapeDataString(orderId);

        // 1. The order must still be pending/due.
        var orders = await pending.GetPendingAsync(ct);
        var order = orders.FirstOrDefault(o =>
            string.Equals(o.OrderId, orderId, StringComparison.Ordinal));

        if (order is null)
            return new OfflineQrResult(
                OfflineQrOutcome.NothingDue,
                "",
                OfflineDocKind.Provisional,
                null);

        // 2. Try fiscal invoice first.
        var invoice = await invoices.GetByInvoiceNumberAsync(orderId);

        bool isFiscal =
            invoice is not null &&
            !invoice.IsProforma &&
            invoice.NormalizedAt is not null &&
            !string.IsNullOrWhiteSpace(invoice.CodeDEFDGI);

        // 3. If not fiscal, use the draft/proforma from pending store.
        // This is important because during checkout the invoice may not exist in DB yet.
        var sourceInvoice = isFiscal
            ? invoice
            : store.GetDraftFor(orderId);

        if (sourceInvoice is null)
        {
            return new OfflineQrResult(
                OfflineQrOutcome.NotFound,
                "",
                OfflineDocKind.Provisional,
                null);
        }

        var company = await uow.Companies.GetCurrentCompanyAsync();
        if (company is null)
            return new OfflineQrResult(
                OfflineQrOutcome.Error,
                "",
                OfflineDocKind.Provisional,
                order);

        var pos = sourceInvoice.PointOfSaleId > 0
            ? await uow.PointsOfSale.GetByIdAsync(sourceInvoice.PointOfSaleId)
            : null;

        var kind = isFiscal
            ? OfflineDocKind.Fiscal
            : OfflineDocKind.Provisional;

        var docs = BuildReceiptDocuments(
            sourceInvoice,
            company,
            pos,
            time,
            sourceInvoice.CurrencyRate,
            asProforma: !isFiscal,
            copies: 1);

        var payload = qr.BuildFor(
            orderId: order.OrderId,
            amount: order.Amount,
            currency: order.Currency,
            kind: kind,
            documents: docs,
            fiscalCode: isFiscal ? sourceInvoice.CodeDEFDGI : null,
            fiscalQr: isFiscal ? sourceInvoice.QRCodeContent : null);

        return new OfflineQrResult(
            OfflineQrOutcome.Ok,
            qr.Encode(payload),
            payload.Kind,
            order);
    }

    private static ReceiptDocument[] BuildReceiptDocuments(
        SFE.Domain.Entities.Invoice invoice,
        SFE.Domain.Entities.Company company,
        SFE.Domain.Entities.PointOfSale? pos,
        ITimeProvider time,
        decimal exchangeRate,
        bool asProforma,
        int copies)
    {
        var docs = new List<ReceiptDocument>
        {
            ReceiptJsonBuilder.Build(
                invoice: invoice,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: exchangeRate,
                isDuplicate: false,
                asProforma: asProforma,
                paperWidthMm: SunmiPaperWidthMm)
        };

        if (copies >= 2)
        {
            docs.Add(ReceiptJsonBuilder.Build(
                invoice: invoice,
                company: company,
                pos: pos,
                time: time,
                exchangeRate: exchangeRate,
                isDuplicate: true,
                asProforma: asProforma,
                paperWidthMm: SunmiPaperWidthMm));
        }

        return docs.ToArray();
    }
}