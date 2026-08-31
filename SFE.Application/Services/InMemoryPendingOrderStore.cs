using SFE.Application.Payments;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

/// <summary>
/// Single-slot store for the LAN API (proforma receipt on demand).
/// </summary>
public sealed class InMemoryPendingOrderStore
{
    private readonly object _gate = new();
    private OrderDto? _currentOrder;
    private Invoice? _draftInvoice;

    public void Set(OrderDto order, Invoice? draft = null)
    {
        lock (_gate)
        {
            _currentOrder = order;
            _draftInvoice = draft;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _currentOrder = null;
            _draftInvoice = null;
        }
    }

    public IReadOnlyList<OrderDto> GetAll()
    {
        lock (_gate)
        {
            return _currentOrder is null
                ? Array.Empty<OrderDto>()
                : new[] { _currentOrder };
        }
    }

    public Invoice? GetDraft()
    {
        lock (_gate) return _draftInvoice;
    }

    public Invoice? GetDraftFor(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return null;

        lock (_gate)
        {
            if (_draftInvoice is null) return null;

            return string.Equals(_draftInvoice.InvoiceNumber, orderId, StringComparison.Ordinal)
                ? _draftInvoice
                : null;
        }
    }

    public bool Remove(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return false;

        lock (_gate)
        {
            if (_currentOrder is not null &&
                string.Equals(_currentOrder.OrderId, orderId, StringComparison.Ordinal))
            {
                _currentOrder = null;
                _draftInvoice = null;
                return true;
            }
            return false;
        }
    }
}