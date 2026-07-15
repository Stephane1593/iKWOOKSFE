using SFE.Application.Payments;

namespace SFE.Application.Interfaces;

/// <summary>Feeds GET /orders. Back this with your InvoiceRepository (proforma/pending invoices).</summary>
public interface IPendingOrderProvider
{
    Task<IReadOnlyList<OrderDto>> GetPendingAsync(CancellationToken ct);

    /// <summary>
    /// Called by the till after a successful payment + normalization, so the terminal's
    /// next poll returns an empty list. Return true if the order was known and cleared,
    /// false if it wasn't found. Implementations backed by DB queries (where "pending"
    /// is derived from invoice state) can safely return true unconditionally — the next
    /// GET /orders will exclude the invoice on its own.
    /// </summary>
    Task<bool> RemoveAsync(string orderId, CancellationToken ct);
}