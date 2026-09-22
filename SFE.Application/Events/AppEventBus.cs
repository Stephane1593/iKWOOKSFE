using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

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

    // Users
    UserCreated,
    UserUpdated,
    UserDeleted,

    // Roles
    RoleCreated,
    RoleUpdated,
    RoleDeleted,

    CategoryCreated,
    CategoryUpdated,
    CategoryDeleted,

    // Fiscal device
    FiscalDeviceStatusChanged,

    // NOUVEAU: Tables
    TableStatusChanged,

    // NOUVEAU: Rafraîchissement manuel
    ForceGlobalRefresh
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

    // 🚨 NOUVEAU: Le client SignalR pour le réseau
    private static HubConnection? _hubConnection;

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

    // 🚨 NOUVEAU: Méthode pour initialiser la connexion au réseau
    public static async Task InitializeNetworkAsync(string serverIp, int port = 5005)
    {
        if (_hubConnection != null) return;

        // Ensure IP is clean (e.g. "192.168.1.10" instead of "Host=...")
        var cleanIp = serverIp;
        if (cleanIp.Contains("Host="))
        {
            var parts = cleanIp.Split(';');
            foreach (var p in parts)
            {
                if (p.StartsWith("Host=")) cleanIp = p.Replace("Host=", "");
            }
        }

        var url = $"http://{cleanIp}:{port}/events";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        // Quand le réseau nous envoie un message, on déclenche nos abonnés locaux
        _hubConnection.On<AppEventArgs>("ReceiveAppEvent", async (args) =>
        {
            Debug.WriteLine($"[AppEventBus] REÇU DU RÉSEAU: {args.Event}");
            await TriggerLocalHandlersAsync(args);
        });

        try
        {
            await _hubConnection.StartAsync();
            Debug.WriteLine($"[AppEventBus] Connecté au serveur SignalR : {url}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppEventBus] Erreur de connexion SignalR: {ex.Message}");
        }
    }

    public static async Task PublishAsync(AppEventArgs args)
    {
        // 1. Déclencher pour l'application locale (WPF)
        await TriggerLocalHandlersAsync(args);

        // 2. 🚨 NOUVEAU: Diffuser sur le réseau si on est connecté !
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            try
            {
                Debug.WriteLine($"[AppEventBus] ENVOI AU RÉSEAU: {args.Event}");
                await _hubConnection.SendAsync("BroadcastAppEvent", args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppEventBus] Erreur d'envoi réseau: {ex.Message}");
            }
        }
    }

    private static async Task TriggerLocalHandlersAsync(AppEventArgs args)
    {
        Func<AppEventArgs, Task>[] snapshot;
        lock (_lock)
            snapshot = _handlers.ToArray();

        foreach (var handler in snapshot)
        {
            try { await handler(args); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppEventBus] Handler error for {args.Event}: {ex.Message}");
            }
        }
    }
}