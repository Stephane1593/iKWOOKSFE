using SFE.Application.Interfaces;
using SFE.Application.Payments;
using SFE.Application.Services;

namespace SFE.WPF.Services;

public sealed class InvoicePendingOrderProvider(
    InMemoryPendingOrderStore store
) : IPendingOrderProvider
{
    public Task<IReadOnlyList<OrderDto>> GetPendingAsync(CancellationToken ct)
        => Task.FromResult(store.GetAll());

    public Task<bool> RemoveAsync(string orderId, CancellationToken ct)
        => Task.FromResult(store.Remove(orderId));
}