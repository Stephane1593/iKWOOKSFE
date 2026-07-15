using SFE.Application.Interfaces;
using SFE.Application.Payments;

namespace SFE.WPF.Services;

public sealed class NullPendingOrderProvider : IPendingOrderProvider
{
    public Task<IReadOnlyList<OrderDto>> GetPendingAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OrderDto>>(Array.Empty<OrderDto>());

    public Task<bool> RemoveAsync(string orderId, CancellationToken ct)
        => Task.FromResult(false);
}