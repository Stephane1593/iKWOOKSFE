using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
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
///   2. Submit always tries primary first regardless of cached state.
///   3. Read operations retry the primary every 15s after failure.
///   4. A successful Submit on primary clears the failed flag immediately.
///   5. Finalize / Cancel always go to the same device that did Submit.
///   6. GetDiagnostics() exposes everything needed to debug fallback issues.
///   7. If DeviceType=MCF and DisableFallback=true → no fallback is ever built.
///   8. Invalidate() is thread-safe (acquires _lock).
///   9. AcquireExclusiveAccessAsync() lets a caller own the COM port
///      exclusively (e.g. for "Test connection") and prevents the resolver
///      from rebuilding while suspended.
///  10. 🆕 Exposes PrimaryDevice / FallbackDevice / CurrentDevice so callers
///      (e.g. SettingsViewModel) can reach the underlying MCF client for
///      diagnostics like GetHealthReportAsync — without reflection.
///  11. 🆕 Exposes GetHealthReportAsync as a convenience that locates the
///      MCF client in either slot and returns its synthetic verdict.
/// </summary>
public class FiscalDeviceResolver : IFiscalDeviceService, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ITimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IFiscalDeviceService? _primaryDevice;
    private IFiscalDeviceService? _fallbackDevice;

    private bool _primaryFailed;
    private DateTimeOffset? _primaryFailedAt;

    private bool _initialized;
    private DeviceType _lastDeviceType;
    private string _lastConfigKey = "";

    private string? _fallbackUnavailableReason;
    private DateTimeOffset? _lastFallbackBuildAttempt;

    private SettingsData? _cachedSettings;

    private readonly Dictionary<string, IFiscalDeviceService> _invoiceDeviceMap = new();
    private readonly object _mapLock = new();

    // Exclusive-access lease counter. While > 0, EnsureDevicesAsync refuses
    // to (re)build devices so a "Test connection" command can own the port.
    private int _suspendCount;

    private static readonly TimeSpan PrimaryRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FallbackRebuildInterval = TimeSpan.FromSeconds(30);

    // Give USB-serial drivers a beat to release the handle after Close().
    private static readonly TimeSpan PortReleaseDelay = TimeSpan.FromMilliseconds(300);

    public string ActiveDeviceLabel
    {
        get
        {
            if (!_primaryFailed) return "Primaire";
            if (_fallbackDevice != null) return "Fallback";
            if (_cachedSettings != null
                && _cachedSettings.DeviceType == DeviceType.Mcf
                && _cachedSettings.DisableFallback)
                return "Primaire (échec, mode MCF strict)";
            return "Primaire (échec, pas de fallback)";
        }
    }

    public bool IsPrimaryFailed => _primaryFailed;
    public DateTimeOffset? PrimaryFailedAt => _primaryFailedAt;
    public bool HasFallback => _fallbackDevice != null;
    public string? FallbackUnavailableReason => _fallbackUnavailableReason;
    public bool IsSuspended => Volatile.Read(ref _suspendCount) > 0;

    // ──────────────────────────────────────────────────────────────
    // 🆕 PUBLIC ACCESSORS — used by ViewModels for direct inspection
    // (e.g. unwrapping to McfSerialClient.GetHealthReportAsync).
    //
    // These are read-only snapshots; callers MUST NOT dispose them.
    // ──────────────────────────────────────────────────────────────

    /// <summary>Underlying primary device, or null if not built yet.</summary>
    public IFiscalDeviceService? PrimaryDevice => _primaryDevice;

    /// <summary>Underlying fallback device, or null if unavailable.</summary>
    public IFiscalDeviceService? FallbackDevice => _fallbackDevice;

    /// <summary>
    /// The device that the next operation would target right now.
    /// Returns the fallback if primary is currently flagged as failed and
    /// a fallback exists, otherwise returns the primary.
    /// </summary>
    public IFiscalDeviceService? CurrentDevice =>
        (_primaryFailed && _fallbackDevice != null) ? _fallbackDevice : _primaryDevice;

    public FiscalDeviceResolver(SettingsService settingsService, ITimeProvider time)
    {
        _settingsService = settingsService;
        _time = time;
    }

    // ══════════════════════════════════════════════════════════════
    // BUILD DEVICES
    // ══════════════════════════════════════════════════════════════

    private async Task EnsureDevicesAsync()
    {
        // If a Test command is currently leasing the port, don't rebuild.
        if (Volatile.Read(ref _suspendCount) > 0)
        {
            throw new InvalidOperationException(
                "Le dispositif fiscal est temporairement réservé par une autre opération " +
                "(test de connexion en cours). Veuillez patienter quelques secondes.");
        }

        var settings = await _settingsService.LoadSettingsAsync();
        _cachedSettings = settings;

        var configKey = settings.DeviceType == DeviceType.EMcf
            ? $"emcf|{settings.EmcfApiUrl}|{settings.EmcfToken}|{settings.CompanyNIF}|{settings.EmcfNIM}|{settings.McfPortName}|{settings.McfBaudRate}|DF={settings.DisableFallback}"
            : $"{settings.DeviceType.ToString().ToLowerInvariant()}|{settings.McfPortName}|{settings.McfBaudRate}|{settings.EmcfApiUrl}|{settings.EmcfToken}|DF={settings.DisableFallback}";

        if (_initialized && _lastDeviceType == settings.DeviceType && _lastConfigKey == configKey)
        {
            TryEnsureFallback(settings);
            return;
        }

        DisposeDevice(_primaryDevice);
        DisposeDevice(_fallbackDevice);
        _primaryDevice = null;
        _fallbackDevice = null;
        _primaryFailed = false;
        _primaryFailedAt = null;
        _fallbackUnavailableReason = null;
        _lastFallbackBuildAttempt = null;

        lock (_mapLock) { _invoiceDeviceMap.Clear(); }

        switch (settings.DeviceType)
        {
            case DeviceType.EMcf:
                _primaryDevice = BuildEmcfDevice(settings);
                Debug.WriteLine("[FiscalResolver] ✓ e-MCF primary built");
                TryEnsureFallback(settings);
                break;

            case DeviceType.Mcf:
                _primaryDevice = BuildMcfDeviceOrThrow(settings);
                Debug.WriteLine("[FiscalResolver] ✓ MCF primary built");
                if (settings.DisableFallback)
                {
                    _fallbackUnavailableReason = "Mode MCF strict — fallback désactivé par configuration.";
                    Debug.WriteLine("[FiscalResolver] ⚠ Fallback DISABLED (MCF strict)");
                }
                else
                {
                    TryEnsureFallback(settings);
                }
                break;

            case DeviceType.Hybrid:
                _primaryDevice = BuildEmcfDevice(settings);
                Debug.WriteLine("[FiscalResolver] ✓ Hybrid: e-MCF primary built");
                if (settings.DisableFallback)
                    Debug.WriteLine("[FiscalResolver] ℹ DisableFallback ignored in Hybrid mode");
                TryEnsureFallback(settings);
                break;
        }

        _lastDeviceType = settings.DeviceType;
        _lastConfigKey = configKey;
        _initialized = true;
    }

    private void TryEnsureFallback(SettingsData settings)
    {
        if (_fallbackDevice != null) return;

        if (settings.DeviceType == DeviceType.Mcf && settings.DisableFallback)
        {
            _fallbackUnavailableReason = "Mode MCF strict — fallback désactivé par configuration.";
            return;
        }

        if (_lastFallbackBuildAttempt.HasValue
            && _time.UtcNow - _lastFallbackBuildAttempt.Value < FallbackRebuildInterval)
        {
            return;
        }

        _lastFallbackBuildAttempt = _time.UtcNow;

        try
        {
            if (settings.DeviceType == DeviceType.EMcf || settings.DeviceType == DeviceType.Hybrid)
            {
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
            else // DeviceType.Mcf with fallback enabled
            {
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

    private IFiscalDeviceService BuildEmcfDevice(SettingsData settings)
        => new EMcfHttpClient(settings.EmcfApiUrl, settings.EmcfToken, settings.CompanyNIF, _time);

    private IFiscalDeviceService BuildMcfDeviceOrThrow(SettingsData settings)
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
        var client = new McfSerialClient(settings.McfPortName, _time, settings.McfBaudRate);
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

    /// <summary>
    /// Thread-safe invalidation. Acquires the resolver's lock so we never
    /// dispose a device that's mid-call.
    /// </summary>
    public void Invalidate()
    {
        _lock.Wait();
        try
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
            Debug.WriteLine("[FiscalResolver] Invalidate() done");
        }
        finally { _lock.Release(); }
    }

    // ══════════════════════════════════════════════════════════════
    // EXCLUSIVE-ACCESS LEASE
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Releases the COM port (and HTTP client) and prevents the resolver from
    /// rebuilding them until the returned handle is disposed. Use this around
    /// a "Test connection" UI action so the test code is the SOLE owner of the
    /// serial port for its duration.
    /// </summary>
    public async Task<IDisposable> AcquireExclusiveAccessAsync(
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DisposeDevice(_primaryDevice);
            DisposeDevice(_fallbackDevice);
            _primaryDevice = null;
            _fallbackDevice = null;
            _initialized = false;
            _primaryFailed = false;
            _primaryFailedAt = null;
            _fallbackUnavailableReason = null;
            _lastFallbackBuildAttempt = null;
            Interlocked.Increment(ref _suspendCount);
            Debug.WriteLine($"[FiscalResolver] Lease ACQUIRED (suspendCount={_suspendCount})");
        }
        finally { _lock.Release(); }

        // Give USB-serial drivers time to release the handle.
        try { await Task.Delay(PortReleaseDelay, ct); }
        catch (OperationCanceledException) { /* still hand back the lease */ }

        return new ExclusiveLease(this);
    }

    private void ReleaseExclusiveAccess()
    {
        var newCount = Interlocked.Decrement(ref _suspendCount);
        if (newCount < 0)
        {
            Interlocked.Exchange(ref _suspendCount, 0);
            newCount = 0;
        }
        Debug.WriteLine($"[FiscalResolver] Lease RELEASED (suspendCount={newCount})");
    }

    private sealed class ExclusiveLease : IDisposable
    {
        private FiscalDeviceResolver? _r;
        public ExclusiveLease(FiscalDeviceResolver r) => _r = r;
        public void Dispose()
        {
            var r = Interlocked.Exchange(ref _r, null);
            r?.ReleaseExclusiveAccess();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 🆕 HEALTH REPORT — convenience pass-through
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the synthetic health verdict from the underlying MCF client,
    /// trying the currently-active device first and falling back to the other
    /// slot if needed. Returns <c>null</c> when no MCF is wired up (pure
    /// e-MCF deployment) or the resolver is suspended for a "Test connection"
    /// command.
    /// </summary>
    public async Task<McfHealthReport?> GetHealthReportAsync(
        McfHealthThresholds? thresholds = null,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException) when (IsSuspended)
            {
                return new McfHealthReport
                {
                    Status = McfHealth.Unknown,
                    CommunicationFailed = true,
                    Summary = "Dispositif réservé par un test de connexion."
                };
            }

            var mcf = FindMcfClient();
            if (mcf == null) return null;

            return await mcf.GetHealthReportAsync(thresholds);
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Looks up an <see cref="McfSerialClient"/> in the currently-active slot
    /// first, then falls back to the other slot. Pure e-MCF deployments
    /// return null.
    /// </summary>
    private McfSerialClient? FindMcfClient()
    {
        if (CurrentDevice is McfSerialClient curr) return curr;
        if (_primaryDevice is McfSerialClient pri) return pri;
        if (_fallbackDevice is McfSerialClient fb) return fb;
        return null;
    }

    // ══════════════════════════════════════════════════════════════
    // DIAGNOSTICS
    // ══════════════════════════════════════════════════════════════

    public class ResolverDiagnostics
    {
        public string PrimaryType { get; set; } = "";
        public bool PrimaryReady { get; set; }
        public bool PrimaryFailed { get; set; }
        public DateTimeOffset? PrimaryFailedAt { get; set; }

        public bool FallbackReady { get; set; }
        public string FallbackType { get; set; } = "";
        public string? FallbackUnavailableReason { get; set; }
        public DateTimeOffset? LastFallbackBuildAttempt { get; set; }

        public string ActiveLabel { get; set; } = "";
        public string[] AvailableSerialPorts { get; set; } = Array.Empty<string>();
        public string? ConfiguredPortName { get; set; }
        public bool DisableFallback { get; set; }
        public bool IsSuspended { get; set; }
    }

    public async Task<ResolverDiagnostics> GetDiagnosticsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException) when (IsSuspended)
            {
                // Suspended for a Test command — that's fine, just report state.
            }

            string primaryType = _cachedSettings?.DeviceType switch
            {
                DeviceType.EMcf => "e-MCF",
                DeviceType.Mcf => "MCF",
                DeviceType.Hybrid => "e-MCF (Hybride)",
                _ => "?"
            };
            string fallbackType = _cachedSettings?.DeviceType switch
            {
                DeviceType.EMcf => "MCF",
                DeviceType.Mcf => "e-MCF",
                DeviceType.Hybrid => "MCF",
                _ => "?"
            };

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
                ConfiguredPortName = _cachedSettings?.McfPortName,
                DisableFallback = _cachedSettings?.DisableFallback ?? false,
                IsSuspended = IsSuspended
            };
        }
        finally { _lock.Release(); }
    }

    // ══════════════════════════════════════════════════════════════
    // PRIMARY RECOVERY HELPERS
    // ══════════════════════════════════════════════════════════════

    private bool ShouldRetryPrimary()
        => _primaryFailed && _primaryFailedAt.HasValue
           && _time.UtcNow - _primaryFailedAt.Value >= PrimaryRetryInterval;

    private void MarkPrimaryFailed()
    {
        if (!_primaryFailed)
        {
            _primaryFailed = true;
            _primaryFailedAt = _time.UtcNow;
            Debug.WriteLine($"[FiscalResolver] Primary marked FAILED at {_primaryFailedAt:HH:mm:ss}");
        }
        else
        {
            _primaryFailedAt = _time.UtcNow;
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
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException ex) when (IsSuspended)
            {
                Debug.WriteLine($"[FiscalResolver] {operationName} skipped: {ex.Message}");
                return buildErrorResult(ex);
            }

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
                        _primaryFailedAt = _time.UtcNow;
                    }
                    catch (Exception retryEx)
                    {
                        Debug.WriteLine($"[FiscalResolver] {operationName}: primary recovery failed: {retryEx.Message}");
                        _primaryFailedAt = _time.UtcNow;
                    }
                }

                try { return await operation(_fallbackDevice); }
                catch (Exception fbEx)
                {
                    Debug.WriteLine($"[FiscalResolver] {operationName}: fallback THREW: {fbEx.Message}");
                    return buildErrorResult(fbEx);
                }
            }

            T result;
            try
            {
                result = await operation(_primaryDevice!);
            }
            catch (Exception primaryEx)
            {
                Debug.WriteLine($"[FiscalResolver] {operationName}: primary THREW: {primaryEx.Message}");
                MarkPrimaryFailed();

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
                        return result;
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
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException ex) when (IsSuspended)
            {
                Debug.WriteLine($"[FiscalResolver] {operationName} skipped (suspended): {ex.Message}");
                return buildErrorResult(ex);
            }

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
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException ex) when (IsSuspended)
            {
                return new FiscalSubmitResult { Success = false, ErrorMessage = ex.Message };
            }

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

            if (_fallbackDevice == null && _cachedSettings != null)
                TryEnsureFallback(_cachedSettings);

            if (_fallbackDevice == null)
            {
                Debug.WriteLine($"[FiscalResolver] SubmitInvoice: NO FALLBACK ({_fallbackUnavailableReason ?? "n/a"})");
                if (!string.IsNullOrEmpty(_fallbackUnavailableReason))
                    primaryResult.ErrorMessage = $"{primaryResult.ErrorMessage} | Fallback indisponible: {_fallbackUnavailableReason}";
                return primaryResult;
            }

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
            try { await EnsureDevicesAsync(); }
            catch (InvalidOperationException ex) when (IsSuspended)
            {
                return new FiscalDeviceDetailedInfo
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }

            var primaryKind = _cachedSettings?.DeviceType == DeviceType.Mcf
                ? FiscalDeviceKind.MCF
                : FiscalDeviceKind.EMcf;
            var fallbackKind = primaryKind == FiscalDeviceKind.EMcf
                ? FiscalDeviceKind.MCF : FiscalDeviceKind.EMcf;

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
                                result.RespondingDevice = RespondingDevice.Primary;
                                result.RespondingDeviceKind = primaryKind;
                                return result;
                            }
                            _primaryFailedAt = _time.UtcNow;
                        }
                        catch { _primaryFailedAt = _time.UtcNow; }
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

            result.RespondingDevice = usedFallback ? RespondingDevice.Fallback : RespondingDevice.Primary;
            result.RespondingDeviceKind = usedFallback ? fallbackKind : primaryKind;
            return result;
        }
        finally { _lock.Release(); }
    }

    public void Dispose()
    {
        try { _lock.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try
        {
            DisposeDevice(_primaryDevice);
            DisposeDevice(_fallbackDevice);
            _primaryDevice = null;
            _fallbackDevice = null;
            _initialized = false;
            lock (_mapLock) { _invoiceDeviceMap.Clear(); }
        }
        finally
        {
            try { _lock.Release(); } catch { }
            _lock.Dispose();
        }
    }
}