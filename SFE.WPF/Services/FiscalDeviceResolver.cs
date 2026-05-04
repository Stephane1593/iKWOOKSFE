using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using System.Diagnostics;
using System.IO.Ports;

namespace SFE.WPF.Services;

/// <summary>
/// Résolveur hybride : construit le dispositif primaire (et optionnellement un fallback)
/// à partir des paramètres, puis bascule automatiquement en cas d'échec.
/// 
/// IMPORTANT: For multi-step operations (Submit → Finalize), the resolver tracks
/// which device was used for Submit and routes Finalize to the SAME device.
/// </summary>
public class FiscalDeviceResolver : IFiscalDeviceService, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IFiscalDeviceService? _primaryDevice;
    private IFiscalDeviceService? _fallbackDevice;
    private bool _primaryFailed;
    private bool _initialized;
    private DateTime? _primaryFailedAt;

    private DeviceType _lastDeviceType;
    private string _lastConfigKey = "";

    // ── Multi-step invoice tracking ──
    private readonly Dictionary<string, IFiscalDeviceService> _invoiceDeviceMap = new();
    private readonly object _mapLock = new();

    /// <summary>Time before attempting to retry the primary after failure (for non-critical ops).</summary>
    private static readonly TimeSpan PrimaryRetryInterval = TimeSpan.FromMinutes(2);

    public string ActiveDeviceLabel => _primaryFailed
        ? (_fallbackDevice != null ? "Fallback" : "Primaire (échec)")
        : "Primaire";

    public bool IsPrimaryFailed => _primaryFailed;
    public DateTime? PrimaryFailedAt => _primaryFailedAt;

    public FiscalDeviceResolver(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    // ══════════════════════════════════════════════════════════════
    // BUILD DEVICES
    // ══════════════════════════════════════════════════════════════

    private async Task EnsureDevicesAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        var configKey = settings.DeviceType == DeviceType.EMcf
            ? $"emcf|{settings.EmcfApiUrl}|{settings.EmcfToken}|{settings.CompanyNIF}|{settings.EmcfNIM}"
            : $"mcf|{settings.McfPortName}|{settings.McfBaudRate}";

        if (_initialized && _lastDeviceType == settings.DeviceType && _lastConfigKey == configKey)
            return;

        DisposeDevice(_primaryDevice);
        DisposeDevice(_fallbackDevice);
        _primaryDevice = null;
        _fallbackDevice = null;
        _primaryFailed = false;
        _primaryFailedAt = null;

        lock (_mapLock) { _invoiceDeviceMap.Clear(); }

        if (settings.DeviceType == DeviceType.EMcf)
        {
            _primaryDevice = BuildEmcfDevice(settings);

            if (!string.IsNullOrWhiteSpace(settings.McfPortName)
                && settings.McfPortName != "(aucun port détecté)"
                && IsPortAvailable(settings.McfPortName))
            {
                try
                {
                    _fallbackDevice = BuildMcfDevice(settings);
                    Debug.WriteLine($"[FiscalResolver] ✓ MCF fallback ready on {settings.McfPortName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FiscalResolver] ✗ MCF fallback build failed: {ex.Message}");
                    _fallbackDevice = null;
                }
            }
            else
            {
                Debug.WriteLine($"[FiscalResolver] MCF fallback skipped — port '{settings.McfPortName}' not available");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(settings.McfPortName)
                && settings.McfPortName != "(aucun port détecté)"
                && IsPortAvailable(settings.McfPortName))
            {
                _primaryDevice = BuildMcfDevice(settings);
            }
            else
            {
                var available = SerialPort.GetPortNames();
                throw new InvalidOperationException(
                    $"Port MCF '{settings.McfPortName}' introuvable. " +
                    $"Ports disponibles: {(available.Length > 0 ? string.Join(", ", available) : "aucun")}.");
            }

            if (!string.IsNullOrWhiteSpace(settings.EmcfApiUrl)
                && !string.IsNullOrWhiteSpace(settings.EmcfToken))
            {
                try
                {
                    _fallbackDevice = BuildEmcfDevice(settings);
                    Debug.WriteLine("[FiscalResolver] ✓ e-MCF fallback ready");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FiscalResolver] ✗ e-MCF fallback build failed: {ex.Message}");
                    _fallbackDevice = null;
                }
            }
        }

        _lastDeviceType = settings.DeviceType;
        _lastConfigKey = configKey;
        _initialized = true;
    }

    private static bool IsPortAvailable(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName)) return false;
        try { return SerialPort.GetPortNames().Contains(portName); }
        catch { return false; }
    }

    private static IFiscalDeviceService BuildEmcfDevice(SettingsData settings)
    {
        return new EMcfHttpClient(settings.EmcfApiUrl, settings.EmcfToken, settings.CompanyNIF);
    }

    private static IFiscalDeviceService BuildMcfDevice(SettingsData settings)
    {
        Debug.WriteLine($"[FiscalResolver] Building MCF port='{settings.McfPortName}', baud={settings.McfBaudRate}");

        if (string.IsNullOrWhiteSpace(settings.McfPortName))
            throw new InvalidOperationException("MCF port name is empty");

        var client = new McfSerialClient(settings.McfPortName, settings.McfBaudRate);
        client.Connect();
        return client;
    }

    private static void DisposeDevice(IFiscalDeviceService? device)
    {
        if (device is IDisposable d)
        {
            try { d.Dispose(); }
            catch (Exception ex) { Debug.WriteLine($"[FiscalResolver] Dispose error: {ex.Message}"); }
        }
    }

    public void Invalidate()
    {
        DisposeDevice(_primaryDevice);
        DisposeDevice(_fallbackDevice);
        _primaryDevice = null;
        _fallbackDevice = null;
        _primaryFailed = false;
        _primaryFailedAt = null;
        _initialized = false;
        lock (_mapLock) { _invoiceDeviceMap.Clear(); }
    }

    // ══════════════════════════════════════════════════════════════
    // PRIMARY RECOVERY
    // ══════════════════════════════════════════════════════════════

    private bool ShouldRetryPrimary()
    {
        if (!_primaryFailed) return false;
        if (_primaryFailedAt == null) return false;
        return DateTime.UtcNow - _primaryFailedAt.Value >= PrimaryRetryInterval;
    }

    private void MarkPrimaryFailed()
    {
        if (!_primaryFailed)
        {
            _primaryFailed = true;
            _primaryFailedAt = DateTime.UtcNow;
            Debug.WriteLine($"[FiscalResolver] Primary marked as FAILED at {_primaryFailedAt:HH:mm:ss}");
        }
    }

    private void MarkPrimaryRecovered()
    {
        if (_primaryFailed)
        {
            Debug.WriteLine("[FiscalResolver] Primary RECOVERED ✓");
        }
        _primaryFailed = false;
        _primaryFailedAt = null;
    }

    // ══════════════════════════════════════════════════════════════
    // CORE EXECUTION — for non-critical ops (GetStatus, GetServerConnection)
    // Uses _primaryFailed to skip primary when it's known to be down.
    // ══════════════════════════════════════════════════════════════

    private async Task<T> ExecuteWithFallbackAsync<T>(
        Func<IFiscalDeviceService, Task<T>> operation,
        Func<T, bool> isSuccess,
        Func<Exception, T> buildErrorResult,
        string operationName)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            // ── If primary previously failed ──
            if (_primaryFailed && _fallbackDevice != null)
            {
                // Periodically attempt to recover the primary
                if (ShouldRetryPrimary())
                {
                    Debug.WriteLine($"[FiscalResolver] Attempting primary recovery for {operationName}...");
                    try
                    {
                        var retryResult = await operation(_primaryDevice!);
                        if (isSuccess(retryResult))
                        {
                            MarkPrimaryRecovered();
                            return retryResult;
                        }
                    }
                    catch (Exception retryEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] Primary recovery failed: {retryEx.Message}");
                        _primaryFailedAt = DateTime.UtcNow; // Reset timer
                    }
                }

                // Use fallback
                try
                {
                    var fallbackResult = await operation(_fallbackDevice);
                    if (!isSuccess(fallbackResult))
                    {
                        Debug.WriteLine($"[FiscalResolver] Fallback returned failure for {operationName}");
                    }
                    return fallbackResult;
                }
                catch (Exception fbEx)
                {
                    Debug.WriteLine($"[FiscalResolver] Fallback THREW for {operationName}: {fbEx.Message}");
                    return buildErrorResult(fbEx);
                }
            }

            // ── Try primary ──
            T result;
            try
            {
                result = await operation(_primaryDevice!);
            }
            catch (Exception primaryEx)
            {
                Debug.WriteLine($"[FiscalResolver] Primary THREW for {operationName}: {primaryEx.Message}");

                if (_fallbackDevice != null)
                {
                    MarkPrimaryFailed();
                    try
                    {
                        return await operation(_fallbackDevice);
                    }
                    catch (Exception fbEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] Fallback also THREW for {operationName}: {fbEx.Message}");
                        return buildErrorResult(fbEx);
                    }
                }
                return buildErrorResult(primaryEx);
            }

            // ── Check result ──
            if (!isSuccess(result) && _fallbackDevice != null)
            {
                Debug.WriteLine($"[FiscalResolver] Primary returned failure for {operationName}, trying fallback");
                MarkPrimaryFailed();

                try
                {
                    var fbResult = await operation(_fallbackDevice);
                    return fbResult;
                }
                catch (Exception fbEx)
                {
                    Debug.WriteLine($"[FiscalResolver] Fallback THREW for {operationName}: {fbEx.Message}");
                    return result; // Return structured primary error
                }
            }

            if (isSuccess(result))
                MarkPrimaryRecovered();

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // INVOICE-AWARE: ensures Finalize/Cancel goes to same device as Submit
    // ══════════════════════════════════════════════════════════════

    private async Task<T> ExecuteOnInvoiceDeviceAsync<T>(
        string uid,
        Func<IFiscalDeviceService, Task<T>> operation,
        Func<Exception, T> buildErrorResult,
        string operationName)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            IFiscalDeviceService? targetDevice;
            lock (_mapLock)
            {
                _invoiceDeviceMap.TryGetValue(uid, out targetDevice);
            }

            if (targetDevice == null)
            {
                // UID not tracked — legacy call or Submit was before app restart.
                // Use the active device as best guess.
                targetDevice = (_primaryFailed && _fallbackDevice != null)
                    ? _fallbackDevice
                    : _primaryDevice!;

                Debug.WriteLine($"[FiscalResolver] UID '{uid}' not in device map — using active device for {operationName}");
            }
            else
            {
                Debug.WriteLine($"[FiscalResolver] UID '{uid}' routed to original Submit device for {operationName}");
            }

            try
            {
                return await operation(targetDevice);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FiscalResolver] {operationName} failed for UID '{uid}': {ex.Message}");

                return buildErrorResult(new InvalidOperationException(
                    $"Le dispositif fiscal ayant enregistré cette facture est inaccessible. " +
                    $"UID: {uid}. Erreur: {ex.Message}", ex));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void TrackInvoiceDevice(string? uid, IFiscalDeviceService device)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        lock (_mapLock)
        {
            _invoiceDeviceMap[uid] = device;

            // Evict old entries to prevent memory growth (keep last 50)
            if (_invoiceDeviceMap.Count > 50)
            {
                var oldest = _invoiceDeviceMap.Keys.First();
                _invoiceDeviceMap.Remove(oldest);
            }
        }
    }

    private void UntrackInvoice(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        lock (_mapLock) { _invoiceDeviceMap.Remove(uid); }
    }

    // ══════════════════════════════════════════════════════════════
    // GetStatusAsync
    // ══════════════════════════════════════════════════════════════

    public Task<FiscalStatusResult> GetStatusAsync()
    {
        return ExecuteWithFallbackAsync(
            device => device.GetStatusAsync(),
            result => result.Success,
            ex => new FiscalStatusResult { Success = false, ErrorMessage = ex.Message },
            "GetStatus");
    }

    // ══════════════════════════════════════════════════════════════
    // SubmitInvoiceAsync
    // ALWAYS tries primary first, regardless of _primaryFailed.
    // Invoice submission is critical — a stale flag from a status
    // check must NEVER silently reroute invoices.
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            // ── ALWAYS try primary first for invoice submission ──
            try
            {
                Debug.WriteLine("[FiscalResolver] SubmitInvoice: trying PRIMARY...");
                var result = await _primaryDevice!.SubmitInvoiceAsync(request);

                if (result.Success)
                {
                    TrackInvoiceDevice(result.Uid, _primaryDevice!);
                    MarkPrimaryRecovered();
                    Debug.WriteLine($"[FiscalResolver] SubmitInvoice: PRIMARY succeeded, UID={result.Uid}");
                    return result;
                }

                // Primary returned a structured failure (not an exception)
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: PRIMARY returned failure: {result.ErrorMessage}");
                MarkPrimaryFailed();

                // Try fallback
                if (_fallbackDevice != null)
                {
                    Debug.WriteLine("[FiscalResolver] SubmitInvoice: trying FALLBACK after primary failure...");
                    try
                    {
                        var fbResult = await _fallbackDevice.SubmitInvoiceAsync(request);

                        if (fbResult.Success)
                        {
                            TrackInvoiceDevice(fbResult.Uid, _fallbackDevice);
                            Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK succeeded, UID={fbResult.Uid}");
                            return fbResult;
                        }

                        Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK also returned failure: {fbResult.ErrorMessage}");
                        // Return fallback's error (more recent attempt)
                        return fbResult;
                    }
                    catch (Exception fbEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK THREW: {fbEx.Message}");
                        // Return the primary's structured error (more informative)
                        return result;
                    }
                }

                // No fallback available — return primary's error
                return result;
            }
            catch (Exception primaryEx)
            {
                // Primary threw an exception (timeout, connection refused, etc.)
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: PRIMARY THREW: {primaryEx.Message}");
                MarkPrimaryFailed();

                if (_fallbackDevice != null)
                {
                    Debug.WriteLine("[FiscalResolver] SubmitInvoice: trying FALLBACK after primary exception...");
                    try
                    {
                        var fbResult = await _fallbackDevice.SubmitInvoiceAsync(request);

                        if (fbResult.Success)
                        {
                            TrackInvoiceDevice(fbResult.Uid, _fallbackDevice);
                            Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK succeeded, UID={fbResult.Uid}");
                            return fbResult;
                        }

                        Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK returned failure: {fbResult.ErrorMessage}");
                        return fbResult;
                    }
                    catch (Exception fbEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK also THREW: {fbEx.Message}");
                        return new FiscalSubmitResult
                        {
                            Success = false,
                            ErrorMessage = $"Primaire: {primaryEx.Message} | Fallback: {fbEx.Message}"
                        };
                    }
                }

                // No fallback
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorMessage = primaryEx.Message
                };
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // FinalizeInvoiceAsync — MUST go to same device that did Submit
    // ══════════════════════════════════════════════════════════════

    public Task<FiscalFinalizeResult> FinalizeInvoiceAsync(string uid, decimal totalTTC, decimal totalTVA)
    {
        return ExecuteOnInvoiceDeviceAsync(
            uid,
            device => device.FinalizeInvoiceAsync(uid, totalTTC, totalTVA),
            ex => new FiscalFinalizeResult { Success = false, ErrorMessage = ex.Message },
            "FinalizeInvoice");
    }

    // ══════════════════════════════════════════════════════════════
    // CancelPendingInvoiceAsync — MUST go to same device that did Submit
    // ══════════════════════════════════════════════════════════════

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        var result = await ExecuteOnInvoiceDeviceAsync(
            uid,
            async device => await device.CancelPendingInvoiceAsync(uid),
            _ => false,
            "CancelPendingInvoice");

        if (result)
            UntrackInvoice(uid);

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetServerConnectionStatusAsync
    // ══════════════════════════════════════════════════════════════

    public Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync()
    {
        return ExecuteWithFallbackAsync(
            device => device.GetServerConnectionStatusAsync(),
            result => result.Success,
            ex => new FiscalServerConnectionResult { Success = false, ErrorMessage = ex.Message },
            "GetServerConnectionStatus");
    }

    // ══════════════════════════════════════════════════════════════
    // GetDetailedInfoAsync — enriched with device labels
    // ══════════════════════════════════════════════════════════════

    public async Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            FiscalDeviceDetailedInfo result;
            bool usedFallback = false;

            try
            {
                if (_primaryFailed && _fallbackDevice != null)
                {
                    // Try primary recovery if interval elapsed
                    if (ShouldRetryPrimary())
                    {
                        try
                        {
                            result = await _primaryDevice!.GetDetailedInfoAsync();
                            if (result.Success)
                            {
                                MarkPrimaryRecovered();
                                result.DeviceTypeLabel = $"{result.DeviceTypeLabel} (récupéré)";
                                return result;
                            }
                        }
                        catch
                        {
                            _primaryFailedAt = DateTime.UtcNow;
                        }
                    }

                    usedFallback = true;
                    result = await _fallbackDevice.GetDetailedInfoAsync();
                }
                else
                {
                    result = await _primaryDevice!.GetDetailedInfoAsync();

                    if (!result.Success && _fallbackDevice != null)
                    {
                        MarkPrimaryFailed();
                        usedFallback = true;
                        try
                        {
                            result = await _fallbackDevice.GetDetailedInfoAsync();
                        }
                        catch (Exception fbEx)
                        {
                            result = new FiscalDeviceDetailedInfo
                            {
                                Success = false,
                                ErrorMessage = $"Primaire et fallback ont échoué. Fallback: {fbEx.Message}"
                            };
                        }
                    }
                    else if (result.Success)
                    {
                        MarkPrimaryRecovered();
                    }
                }
            }
            catch (Exception primaryEx)
            {
                if (_fallbackDevice != null)
                {
                    MarkPrimaryFailed();
                    usedFallback = true;
                    try
                    {
                        result = await _fallbackDevice.GetDetailedInfoAsync();
                    }
                    catch (Exception fbEx)
                    {
                        result = new FiscalDeviceDetailedInfo
                        {
                            Success = false,
                            ErrorMessage = $"Primaire: {primaryEx.Message} | Fallback: {fbEx.Message}"
                        };
                    }
                }
                else
                {
                    result = new FiscalDeviceDetailedInfo
                    {
                        Success = false,
                        ErrorMessage = primaryEx.Message
                    };
                }
            }

            // Enrich label
            if (result.Success)
            {
                if (usedFallback)
                    result.DeviceTypeLabel = $"{result.DeviceTypeLabel} (fallback)";
                else if (_fallbackDevice != null)
                    result.DeviceTypeLabel = $"{result.DeviceTypeLabel} (hybride)";
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // IDisposable
    // ══════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _lock.Dispose();
        DisposeDevice(_primaryDevice);
        DisposeDevice(_fallbackDevice);
        _primaryDevice = null;
        _fallbackDevice = null;
        _initialized = false;
        lock (_mapLock) { _invoiceDeviceMap.Clear(); }
    }
}