using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using System.Diagnostics;
using System.IO.Ports;

namespace SFE.WPF.Services;

/// <summary>
/// Hybrid resolver: builds primary + (optional) fallback fiscal devices and
/// transparently fails over.
///
/// KEY GUARANTEES:
///   1. Fallback is built LAZILY and RETRIED on every call if it failed before.
///      A serial port being unavailable at startup no longer permanently
///      disables fallback.
///   2. Submit always tries primary first regardless of cached state.
///   3. Read operations (status/info) retry the primary every 15 seconds
///      after a failure, so recovery is fast once the network returns.
///   4. A successful Submit on primary clears the failed flag immediately,
///      so subsequent reads stop using the fallback.
///   5. Finalize / Cancel always go to the same device that did Submit.
///   6. GetDiagnostics() exposes everything needed to debug fallback issues.
/// </summary>
public class FiscalDeviceResolver : IFiscalDeviceService, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IFiscalDeviceService? _primaryDevice;
    private IFiscalDeviceService? _fallbackDevice;

    private bool _primaryFailed;
    private DateTime? _primaryFailedAt;

    private bool _initialized;
    private DeviceType _lastDeviceType;
    private string _lastConfigKey = "";

    // Last reason fallback build was skipped/failed — surfaced via diagnostics
    private string? _fallbackUnavailableReason;
    private DateTime? _lastFallbackBuildAttempt;

    private SettingsData? _cachedSettings;

    private readonly Dictionary<string, IFiscalDeviceService> _invoiceDeviceMap = new();
    private readonly object _mapLock = new();

    // Lower retry interval: e-MCF connect timeout is now 5s (in EMcfHttpClient),
    // so probing the primary every 15s is cheap and gives near-instant recovery.
    private static readonly TimeSpan PrimaryRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FallbackRebuildInterval = TimeSpan.FromSeconds(30);

    public string ActiveDeviceLabel => _primaryFailed
        ? (_fallbackDevice != null ? "Fallback" : "Primaire (échec, pas de fallback)")
        : "Primaire";

    public bool IsPrimaryFailed => _primaryFailed;
    public DateTime? PrimaryFailedAt => _primaryFailedAt;
    public bool HasFallback => _fallbackDevice != null;
    public string? FallbackUnavailableReason => _fallbackUnavailableReason;

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
        _cachedSettings = settings;

        var configKey = settings.DeviceType == DeviceType.EMcf
            ? $"emcf|{settings.EmcfApiUrl}|{settings.EmcfToken}|{settings.CompanyNIF}|{settings.EmcfNIM}|{settings.McfPortName}|{settings.McfBaudRate}"
            : $"mcf|{settings.McfPortName}|{settings.McfBaudRate}|{settings.EmcfApiUrl}|{settings.EmcfToken}";

        if (_initialized && _lastDeviceType == settings.DeviceType && _lastConfigKey == configKey)
        {
            // Configuration unchanged — but try to LAZILY rebuild fallback if it's missing
            TryEnsureFallback(settings);
            return;
        }

        // Configuration changed — rebuild everything
        DisposeDevice(_primaryDevice);
        DisposeDevice(_fallbackDevice);
        _primaryDevice = null;
        _fallbackDevice = null;
        _primaryFailed = false;
        _primaryFailedAt = null;
        _fallbackUnavailableReason = null;
        _lastFallbackBuildAttempt = null;

        lock (_mapLock) { _invoiceDeviceMap.Clear(); }

        if (settings.DeviceType == DeviceType.EMcf)
        {
            _primaryDevice = BuildEmcfDevice(settings);
            Debug.WriteLine("[FiscalResolver] ✓ e-MCF primary built");
            TryEnsureFallback(settings);  // MCF fallback (lazy)
        }
        else
        {
            _primaryDevice = BuildMcfDeviceOrThrow(settings);  // MCF primary must succeed
            Debug.WriteLine("[FiscalResolver] ✓ MCF primary built");
            TryEnsureFallback(settings);  // e-MCF fallback (lazy)
        }

        _lastDeviceType = settings.DeviceType;
        _lastConfigKey = configKey;
        _initialized = true;
    }

    /// <summary>
    /// Lazily builds the fallback device. Safe to call repeatedly — if a previous
    /// attempt failed, it retries every <see cref="FallbackRebuildInterval"/>.
    /// </summary>
    private void TryEnsureFallback(SettingsData settings)
    {
        if (_fallbackDevice != null) return;  // already built

        // Don't hammer if we just tried
        if (_lastFallbackBuildAttempt.HasValue
            && DateTime.UtcNow - _lastFallbackBuildAttempt.Value < FallbackRebuildInterval)
        {
            return;
        }

        _lastFallbackBuildAttempt = DateTime.UtcNow;

        try
        {
            if (settings.DeviceType == DeviceType.EMcf)
            {
                // Fallback = MCF
                if (string.IsNullOrWhiteSpace(settings.McfPortName)
                    || settings.McfPortName == "(aucun port détecté)")
                {
                    _fallbackUnavailableReason = "Aucun port MCF configuré dans les paramètres";
                    Debug.WriteLine($"[FiscalResolver] ✗ MCF fallback skipped: {_fallbackUnavailableReason}");
                    return;
                }

                var available = SerialPort.GetPortNames();
                if (!available.Contains(settings.McfPortName))
                {
                    _fallbackUnavailableReason =
                        $"Port MCF '{settings.McfPortName}' introuvable. " +
                        $"Disponibles: {(available.Length > 0 ? string.Join(", ", available) : "aucun")}";
                    Debug.WriteLine($"[FiscalResolver] ✗ MCF fallback skipped: {_fallbackUnavailableReason}");
                    return;
                }

                _fallbackDevice = BuildMcfDeviceOrThrow(settings);
                _fallbackUnavailableReason = null;
                Debug.WriteLine($"[FiscalResolver] ✓ MCF fallback ready on {settings.McfPortName}");
            }
            else
            {
                // Fallback = e-MCF
                if (string.IsNullOrWhiteSpace(settings.EmcfApiUrl)
                    || string.IsNullOrWhiteSpace(settings.EmcfToken))
                {
                    _fallbackUnavailableReason = "Paramètres e-MCF (URL/Token) manquants";
                    Debug.WriteLine($"[FiscalResolver] ✗ e-MCF fallback skipped: {_fallbackUnavailableReason}");
                    return;
                }

                _fallbackDevice = BuildEmcfDevice(settings);
                _fallbackUnavailableReason = null;
                Debug.WriteLine("[FiscalResolver] ✓ e-MCF fallback ready");
            }
        }
        catch (Exception ex)
        {
            _fallbackUnavailableReason = $"{ex.GetType().Name}: {ex.Message}";
            Debug.WriteLine($"[FiscalResolver] ✗ Fallback build failed: {_fallbackUnavailableReason}");
            DisposeDevice(_fallbackDevice);
            _fallbackDevice = null;
        }
    }

    private static IFiscalDeviceService BuildEmcfDevice(SettingsData settings)
        => new EMcfHttpClient(settings.EmcfApiUrl, settings.EmcfToken, settings.CompanyNIF);

    private static IFiscalDeviceService BuildMcfDeviceOrThrow(SettingsData settings)
    {
        if (string.IsNullOrWhiteSpace(settings.McfPortName))
            throw new InvalidOperationException("Le nom du port MCF est vide");

        if (settings.McfPortName == "(aucun port détecté)")
            throw new InvalidOperationException("Aucun port série n'a été détecté");

        var available = SerialPort.GetPortNames();
        if (!available.Contains(settings.McfPortName))
        {
            throw new InvalidOperationException(
                $"Port MCF '{settings.McfPortName}' introuvable. " +
                $"Disponibles: {(available.Length > 0 ? string.Join(", ", available) : "aucun")}");
        }

        Debug.WriteLine($"[FiscalResolver] Building MCF port='{settings.McfPortName}', baud={settings.McfBaudRate}");
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
        _fallbackUnavailableReason = null;
        _lastFallbackBuildAttempt = null;
        lock (_mapLock) { _invoiceDeviceMap.Clear(); }
    }

    // ══════════════════════════════════════════════════════════════
    // DIAGNOSTICS — call this from a Settings/Diagnostics screen
    // ══════════════════════════════════════════════════════════════

    public class ResolverDiagnostics
    {
        public string PrimaryType { get; set; } = "";
        public bool PrimaryReady { get; set; }
        public bool PrimaryFailed { get; set; }
        public DateTime? PrimaryFailedAt { get; set; }

        public bool FallbackReady { get; set; }
        public string FallbackType { get; set; } = "";
        public string? FallbackUnavailableReason { get; set; }
        public DateTime? LastFallbackBuildAttempt { get; set; }

        public string ActiveLabel { get; set; } = "";
        public string[] AvailableSerialPorts { get; set; } = Array.Empty<string>();
        public string? ConfiguredPortName { get; set; }
    }

    public async Task<ResolverDiagnostics> GetDiagnosticsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            string primaryType = _cachedSettings?.DeviceType == DeviceType.EMcf ? "e-MCF" : "MCF";
            string fallbackType = _cachedSettings?.DeviceType == DeviceType.EMcf ? "MCF" : "e-MCF";

            return new ResolverDiagnostics
            {
                PrimaryType = primaryType,
                PrimaryReady = _primaryDevice != null,
                PrimaryFailed = _primaryFailed,
                PrimaryFailedAt = _primaryFailedAt,
                FallbackReady = _fallbackDevice != null,
                FallbackType = fallbackType,
                FallbackUnavailableReason = _fallbackUnavailableReason,
                LastFallbackBuildAttempt = _lastFallbackBuildAttempt,
                ActiveLabel = ActiveDeviceLabel,
                AvailableSerialPorts = SerialPort.GetPortNames(),
                ConfiguredPortName = _cachedSettings?.McfPortName
            };
        }
        finally { _lock.Release(); }
    }

    // ══════════════════════════════════════════════════════════════
    // PRIMARY RECOVERY HELPERS
    // ══════════════════════════════════════════════════════════════

    private bool ShouldRetryPrimary()
        => _primaryFailed && _primaryFailedAt.HasValue
           && DateTime.UtcNow - _primaryFailedAt.Value >= PrimaryRetryInterval;

    private void MarkPrimaryFailed()
    {
        if (!_primaryFailed)
        {
            _primaryFailed = true;
            _primaryFailedAt = DateTime.UtcNow;
            Debug.WriteLine($"[FiscalResolver] Primary marked FAILED at {_primaryFailedAt:HH:mm:ss}");
        }
        else
        {
            // Refresh timestamp so the 15s cooldown is measured from the last failure
            _primaryFailedAt = DateTime.UtcNow;
        }
    }

    private void MarkPrimaryRecovered()
    {
        if (_primaryFailed)
            Debug.WriteLine("[FiscalResolver] Primary RECOVERED ✓");
        _primaryFailed = false;
        _primaryFailedAt = null;
    }

    // ══════════════════════════════════════════════════════════════
    // CORE EXECUTION (non-critical ops)
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

            // ── If primary was previously down: prefer fallback, but probe primary every 15s ──
            if (_primaryFailed && _fallbackDevice != null)
            {
                if (ShouldRetryPrimary())
                {
                    Debug.WriteLine($"[FiscalResolver] {operationName}: probing primary recovery");
                    try
                    {
                        var retry = await operation(_primaryDevice!);
                        if (isSuccess(retry))
                        {
                            MarkPrimaryRecovered();
                            return retry;
                        }
                        // Failed again — refresh cooldown timestamp
                        _primaryFailedAt = DateTime.UtcNow;
                    }
                    catch (Exception retryEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] {operationName}: primary recovery failed: {retryEx.Message}");
                        _primaryFailedAt = DateTime.UtcNow;
                    }
                }

                try { return await operation(_fallbackDevice); }
                catch (Exception fbEx)
                {
                    Debug.WriteLine($"[FiscalResolver] {operationName}: fallback THREW: {fbEx.Message}");
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
                Debug.WriteLine($"[FiscalResolver] {operationName}: primary THREW: {primaryEx.Message}");
                MarkPrimaryFailed();

                // Lazy-build fallback if it doesn't exist
                if (_fallbackDevice == null && _cachedSettings != null)
                    TryEnsureFallback(_cachedSettings);

                if (_fallbackDevice != null)
                {
                    try { return await operation(_fallbackDevice); }
                    catch (Exception fbEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] {operationName}: fallback also THREW: {fbEx.Message}");
                        return buildErrorResult(fbEx);
                    }
                }
                return buildErrorResult(primaryEx);
            }

            // ── Primary returned a structured failure ──
            if (!isSuccess(result))
            {
                Debug.WriteLine($"[FiscalResolver] {operationName}: primary returned failure");
                MarkPrimaryFailed();

                if (_fallbackDevice == null && _cachedSettings != null)
                    TryEnsureFallback(_cachedSettings);

                if (_fallbackDevice != null)
                {
                    try
                    {
                        var fb = await operation(_fallbackDevice);
                        Debug.WriteLine($"[FiscalResolver] {operationName}: fallback success={isSuccess(fb)}");
                        return fb;
                    }
                    catch (Exception fbEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] {operationName}: fallback THREW: {fbEx.Message}");
                        return result; // primary error is more informative
                    }
                }

                Debug.WriteLine($"[FiscalResolver] {operationName}: NO FALLBACK ({_fallbackUnavailableReason ?? "n/a"})");
                return result;
            }

            MarkPrimaryRecovered();
            return result;
        }
        finally { _lock.Release(); }
    }

    // ══════════════════════════════════════════════════════════════
    // INVOICE-AWARE ROUTING
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
            lock (_mapLock) { _invoiceDeviceMap.TryGetValue(uid, out targetDevice); }

            if (targetDevice == null)
            {
                targetDevice = (_primaryFailed && _fallbackDevice != null)
                    ? _fallbackDevice
                    : _primaryDevice!;
                Debug.WriteLine($"[FiscalResolver] UID '{uid}' not in map — using active device for {operationName}");
            }
            else
            {
                Debug.WriteLine($"[FiscalResolver] UID '{uid}' routed to original Submit device for {operationName}");
            }

            try { return await operation(targetDevice); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FiscalResolver] {operationName} failed for UID '{uid}': {ex.Message}");
                return buildErrorResult(new InvalidOperationException(
                    $"Le dispositif ayant enregistré la facture est inaccessible. UID: {uid}. {ex.Message}", ex));
            }
        }
        finally { _lock.Release(); }
    }

    private void TrackInvoiceDevice(string? uid, IFiscalDeviceService device)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        lock (_mapLock)
        {
            _invoiceDeviceMap[uid] = device;
            if (_invoiceDeviceMap.Count > 50)
                _invoiceDeviceMap.Remove(_invoiceDeviceMap.Keys.First());
        }
    }

    private void UntrackInvoice(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        lock (_mapLock) { _invoiceDeviceMap.Remove(uid); }
    }

    // ══════════════════════════════════════════════════════════════
    // PUBLIC INTERFACE
    // ══════════════════════════════════════════════════════════════

    public Task<FiscalStatusResult> GetStatusAsync()
        => ExecuteWithFallbackAsync(
            d => d.GetStatusAsync(),
            r => r.Success,
            ex => new FiscalStatusResult { Success = false, ErrorMessage = ex.Message },
            "GetStatus");

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureDevicesAsync();

            // Always try primary first for invoice submission — this is also
            // the fastest way to detect primary recovery, since a successful
            // Submit immediately clears _primaryFailed for all subsequent calls.
            FiscalSubmitResult primaryResult;
            try
            {
                Debug.WriteLine("[FiscalResolver] SubmitInvoice: trying PRIMARY");
                primaryResult = await _primaryDevice!.SubmitInvoiceAsync(request);

                if (primaryResult.Success)
                {
                    TrackInvoiceDevice(primaryResult.Uid, _primaryDevice!);
                    MarkPrimaryRecovered();
                    return primaryResult;
                }

                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: PRIMARY failure: {primaryResult.ErrorMessage}");
                MarkPrimaryFailed();
            }
            catch (Exception primaryEx)
            {
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: PRIMARY THREW: {primaryEx.Message}");
                MarkPrimaryFailed();
                primaryResult = new FiscalSubmitResult { Success = false, ErrorMessage = primaryEx.Message };
            }

            // Lazy-build fallback if missing
            if (_fallbackDevice == null && _cachedSettings != null)
                TryEnsureFallback(_cachedSettings);

            if (_fallbackDevice == null)
            {
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: NO FALLBACK ({_fallbackUnavailableReason ?? "n/a"})");
                if (!string.IsNullOrEmpty(_fallbackUnavailableReason))
                    primaryResult.ErrorMessage = $"{primaryResult.ErrorMessage} | Fallback indisponible: {_fallbackUnavailableReason}";
                return primaryResult;
            }

            // Try fallback
            try
            {
                Debug.WriteLine("[FiscalResolver] SubmitInvoice: trying FALLBACK");
                var fb = await _fallbackDevice.SubmitInvoiceAsync(request);
                if (fb.Success)
                {
                    TrackInvoiceDevice(fb.Uid, _fallbackDevice);
                    return fb;
                }
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK failure: {fb.ErrorMessage}");
                return fb;
            }
            catch (Exception fbEx)
            {
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: FALLBACK THREW: {fbEx.Message}");
                return new FiscalSubmitResult
                {
                    Success = false,
                    ErrorMessage = $"Primaire: {primaryResult.ErrorMessage} | Fallback: {fbEx.Message}"
                };
            }
        }
        finally { _lock.Release(); }
    }

    public Task<FiscalFinalizeResult> FinalizeInvoiceAsync(string uid, decimal totalTTC, decimal totalTVA)
        => ExecuteOnInvoiceDeviceAsync(
            uid,
            d => d.FinalizeInvoiceAsync(uid, totalTTC, totalTVA),
            ex => new FiscalFinalizeResult { Success = false, ErrorMessage = ex.Message },
            "FinalizeInvoice");

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        var ok = await ExecuteOnInvoiceDeviceAsync(
            uid,
            async d => await d.CancelPendingInvoiceAsync(uid),
            _ => false,
            "CancelPendingInvoice");
        if (ok) UntrackInvoice(uid);
        return ok;
    }

    public Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync()
        => ExecuteWithFallbackAsync(
            d => d.GetServerConnectionStatusAsync(),
            r => r.Success,
            ex => new FiscalServerConnectionResult { Success = false, ErrorMessage = ex.Message },
            "GetServerConnectionStatus");

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
                            _primaryFailedAt = DateTime.UtcNow;
                        }
                        catch { _primaryFailedAt = DateTime.UtcNow; }
                    }
                    usedFallback = true;
                    result = await _fallbackDevice.GetDetailedInfoAsync();
                }
                else
                {
                    result = await _primaryDevice!.GetDetailedInfoAsync();

                    if (!result.Success)
                    {
                        MarkPrimaryFailed();
                        if (_fallbackDevice == null && _cachedSettings != null)
                            TryEnsureFallback(_cachedSettings);

                        if (_fallbackDevice != null)
                        {
                            usedFallback = true;
                            try { result = await _fallbackDevice.GetDetailedInfoAsync(); }
                            catch (Exception fbEx)
                            {
                                result = new FiscalDeviceDetailedInfo
                                {
                                    Success = false,
                                    ErrorMessage = $"Primaire et fallback ont échoué. Fallback: {fbEx.Message}"
                                };
                            }
                        }
                    }
                    else
                    {
                        MarkPrimaryRecovered();
                    }
                }
            }
            catch (Exception primaryEx)
            {
                MarkPrimaryFailed();
                if (_fallbackDevice == null && _cachedSettings != null)
                    TryEnsureFallback(_cachedSettings);

                if (_fallbackDevice != null)
                {
                    usedFallback = true;
                    try { result = await _fallbackDevice.GetDetailedInfoAsync(); }
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
                        ErrorMessage = $"{primaryEx.Message} | Fallback indisponible: {_fallbackUnavailableReason ?? "n/a"}"
                    };
                }
            }

            if (result.Success)
            {
                if (usedFallback)
                    result.DeviceTypeLabel = $"{result.DeviceTypeLabel} (fallback)";
                else if (_fallbackDevice != null)
                    result.DeviceTypeLabel = $"{result.DeviceTypeLabel} (hybride)";
            }

            return result;
        }
        finally { _lock.Release(); }
    }

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