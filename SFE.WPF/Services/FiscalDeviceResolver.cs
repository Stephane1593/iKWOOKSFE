using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;

namespace SFE.WPF.Services;

/// <summary>
/// Routes fiscal calls to MCF or e-MCF based on current settings.
/// Injected as IFiscalDeviceService into InvoiceService.
/// </summary>
public class FiscalDeviceResolver : IFiscalDeviceService, IDisposable
{
    private readonly SettingsService _settingsService;

    private IFiscalDeviceService? _currentDevice;
    private DeviceType _lastDeviceType;
    private string _lastConnectionKey = "";

    public FiscalDeviceResolver(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private async Task<IFiscalDeviceService> GetDeviceAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        string connectionKey = settings.DeviceType == DeviceType.EMcf
            ? $"emcf:{settings.EmcfApiUrl}:{settings.EmcfToken}"
            : $"mcf:{settings.McfPortName}:{settings.McfBaudRate}";

        // Reuse if same config
        if (_currentDevice != null && connectionKey == _lastConnectionKey)
            return _currentDevice;

        // Dispose old device
        if (_currentDevice is IDisposable disposable)
            disposable.Dispose();

        // Create new device based on settings
        if (settings.DeviceType == DeviceType.EMcf)
        {
            _currentDevice = new EMcfHttpClient(
                settings.EmcfApiUrl,
                settings.EmcfToken,
                settings.CompanyNIF);
        }
        else
        {
            var mcf = new McfSerialClient(settings.McfPortName, settings.McfBaudRate);
            mcf.Connect();
            _currentDevice = mcf;
        }

        _lastDeviceType = settings.DeviceType;
        _lastConnectionKey = connectionKey;
        return _currentDevice;
    }

    public async Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request)
    {
        var device = await GetDeviceAsync();
        return await device.SubmitInvoiceAsync(request);
    }

    public async Task<FiscalFinalizeResult> FinalizeInvoiceAsync(
        string uid, decimal totalTTC, decimal totalTVA)
    {
        var device = await GetDeviceAsync();
        return await device.FinalizeInvoiceAsync(uid, totalTTC, totalTVA);
    }

    public async Task<FiscalStatusResult> GetStatusAsync()
    {
        var device = await GetDeviceAsync();
        return await device.GetStatusAsync();
    }

    public void Dispose()
    {
        if (_currentDevice is IDisposable disposable)
            disposable.Dispose();
    }

    public async Task<bool> CancelPendingInvoiceAsync(string uid)
    {
        var device = await GetDeviceAsync();
        return await device.CancelPendingInvoiceAsync(uid);
    }
}