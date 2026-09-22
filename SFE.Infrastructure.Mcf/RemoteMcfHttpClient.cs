using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;

namespace SFE.Infrastructure.Mcf;

public class RemoteMcfHttpClient : IFiscalDeviceService, IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public RemoteMcfHttpClient(string serverUrl, ITimeProvider time)
    {
        if (!serverUrl.EndsWith("/")) serverUrl += "/";

        _http = new HttpClient
        {
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(90) // Délai étendu pour le 38h
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<FiscalStatusResult> GetStatusAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/fiscal/status");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FiscalStatusResult>(_jsonOptions)
                   ?? new FiscalStatusResult { Success = false };
        }
        catch (Exception ex) { return new FiscalStatusResult { Success = false, ErrorMessage = $"Erreur Proxy MCF : {ex.Message}" }; }
    }

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/fiscal/submit", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FiscalSubmitResult>(_jsonOptions)
                   ?? new FiscalSubmitResult { Success = false };
        }
        catch (Exception ex) { return new FiscalSubmitResult { Success = false, ErrorMessage = $"Erreur Proxy MCF : {ex.Message}" }; }
    }

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(string uid, decimal totalTTC, decimal totalTVA)
    {
        try
        {
            var payload = new { TotalTTC = totalTTC, TotalTVA = totalTVA };
            var response = await _http.PostAsJsonAsync($"api/fiscal/finalize/{Uri.EscapeDataString(uid)}", payload, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FiscalFinalizeResult>(_jsonOptions)
                   ?? new FiscalFinalizeResult { Success = false };
        }
        catch (Exception ex) { return new FiscalFinalizeResult { Success = false, ErrorMessage = $"Erreur Proxy MCF : {ex.Message}" }; }
    }

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        try
        {
            var response = await _http.PostAsync($"api/fiscal/cancel/{Uri.EscapeDataString(uid)}", null);
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.TryGetProperty("success", out var s) && s.GetBoolean();
        }
        catch { return false; }
    }

    public async Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/fiscal/server-status");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FiscalServerConnectionResult>(_jsonOptions)
                   ?? new FiscalServerConnectionResult { Success = false };
        }
        catch (Exception ex) { return new FiscalServerConnectionResult { Success = false, ErrorMessage = $"Erreur Proxy MCF : {ex.Message}" }; }
    }

    public async Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/fiscal/detailed-info");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FiscalDeviceDetailedInfo>(_jsonOptions)
                   ?? new FiscalDeviceDetailedInfo { Success = false };
        }
        catch (Exception ex) { return new FiscalDeviceDetailedInfo { Success = false, ErrorMessage = $"Erreur Proxy MCF : {ex.Message}" }; }
    }

    // 🆕 Proxy pour le rapport de santé spécifique au MCF
    public async Task<McfHealthReport> GetHealthReportAsync(McfHealthThresholds? thresholds = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/fiscal/health", thresholds, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<McfHealthReport>(_jsonOptions)
                   ?? new McfHealthReport { Status = McfHealth.Unknown };
        }
        catch (Exception ex)
        {
            return new McfHealthReport { Status = McfHealth.Unknown, Summary = $"Erreur Proxy: {ex.Message}" };
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}