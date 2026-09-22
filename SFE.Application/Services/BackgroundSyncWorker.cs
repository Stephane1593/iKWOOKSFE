using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SFE.Application.Helpers;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SFE.Application.Services;

public class BackgroundSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITimeProvider _time;
    private readonly HttpClient _httpClient;

    public BackgroundSyncWorker(IServiceProvider serviceProvider, ITimeProvider time)
    {
        _serviceProvider = serviceProvider;
        _time = time;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Debug.WriteLine("[Postman] Facteur en attente...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Lire le sticky note
                var config = LocalNetworkConfig.Load();

                // 2. Le facteur ne travaille que si la Sync est activée ET qu'il est en mode Local
                if (config.DatabaseProvider == "SQLite" && config.EnableSync && !string.IsNullOrWhiteSpace(config.SyncServerUrl))
                {
                    await PerformSyncAsync(config.SyncServerUrl, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Postman] Erreur durant la tournée : {ex.Message}");
            }

            // Le facteur se repose 10 secondes avant sa prochaine tournée
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task PerformSyncAsync(string serverUrl, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // 1. Ramasser le courrier (Récupérer les factures non envoyées)
        var unsyncedInvoices = await uow.Invoices.GetUnsyncedInvoicesAsync(20);

        if (!unsyncedInvoices.Any()) return; // Rien à envoyer

        Debug.WriteLine($"[Postman] {unsyncedInvoices.Count} facture(s) à envoyer à {serverUrl} !");

        // 2. Livrer le courrier (Envoyer à l'API centrale)
        var cleanUrl = serverUrl.Trim();

        // Si l'utilisateur a oublié le "http://", on l'ajoute pour lui
        if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            cleanUrl = "http://" + cleanUrl;
        }

        var url = $"{cleanUrl.TrimEnd('/')}/api/sync/push";

        try
        {
            // On dit au Facteur d'ignorer le "Miroir Infini" (Reference Cycles)
            var jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            var response = await _httpClient.PostAsJsonAsync(url, unsyncedInvoices, jsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                // 3. Tamponner le courrier comme "Livré"
                var now = _time.UtcNow;
                var ids = unsyncedInvoices.Select(i => i.Id).ToList();

                await uow.Invoices.MarkAsSyncedAsync(ids, now);
                Debug.WriteLine("[Postman] Livraison réussie !");
            }
            else
            {
                Debug.WriteLine($"[Postman] Le serveur a refusé le colis (Statut {response.StatusCode}).");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Postman] Impossible d'atteindre le serveur (Réseau coupé ?). {ex.Message}");
        }
    }
}