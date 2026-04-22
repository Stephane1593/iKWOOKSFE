// SFE.Application/Events/AppEventBus.cs
namespace SFE.Application.Events;

public enum AppEvent
{
    // Products
    ProductCreated,
    ProductUpdated,
    ProductDeleted,

    // Stock
    StockUpdated,

    // Transfers
    StockTransferCreated,
    StockTransferShipped,
    StockTransferReceived,
    StockTransferCancelled,

    // 🆕 Users
    UserCreated,
    UserUpdated,
    UserDeleted,

    // 🆕 Roles
    RoleCreated,
    RoleUpdated,
    RoleDeleted,

    CategoryCreated,
    CategoryUpdated,
    CategoryDeleted,
}

public class AppEventArgs
{
    public AppEvent Event { get; init; }
    public string? EntityId { get; init; }
}

public static class AppEventBus
{
    private static readonly List<Func<AppEventArgs, Task>> _handlers = new();
    private static readonly object _lock = new();

    public static void Subscribe(Func<AppEventArgs, Task> handler)
    {
        lock (_lock)
            _handlers.Add(handler);
    }

    public static void Unsubscribe(Func<AppEventArgs, Task> handler)
    {
        lock (_lock)
            _handlers.Remove(handler);
    }

    public static async Task PublishAsync(AppEventArgs args)
    {
        Func<AppEventArgs, Task>[] snapshot;
        lock (_lock)
            snapshot = _handlers.ToArray();

        foreach (var handler in snapshot)
        {
            try { await handler(args); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AppEventBus] Handler error for {args.Event}: {ex.Message}");
            }
        }
    }
}