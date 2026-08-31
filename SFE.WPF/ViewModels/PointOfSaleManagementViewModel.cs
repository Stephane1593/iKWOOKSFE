using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using SFE.WPF.Helpers;
using SFE.WPF.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Media;
using SFE.Licensing.Domain;
using SFE.Licensing.Local;

namespace SFE.WPF.ViewModels;

// ════════════════════════════════════════════════════════════════
//  COM PORT INFO
// ════════════════════════════════════════════════════════════════
public partial class ComPortInfo : ObservableObject
{
    public string Name { get; }
    public ComPortStatus Status { get; }
    public string StatusLabel { get; }
    public Brush StatusColor { get; }
    public Brush TextColor { get; }

    public ComPortInfo(string name, ComPortStatus status)
    {
        Name = name;
        Status = status;
        (StatusLabel, StatusColor, TextColor) = status switch
        {
            ComPortStatus.Available => (
                "Disponible",
                (Brush)new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                (Brush)new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x4E))),
            ComPortStatus.InUse => (
                "Occupé",
                (Brush)new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                (Brush)new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E))),
            ComPortStatus.NotFound => (
                "Introuvable",
                (Brush)new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                (Brush)new SolidColorBrush(Color.FromRgb(0x9C, 0x1C, 0x1C))),
            ComPortStatus.Probing => (
                "Analyse…",
                (Brush)new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                (Brush)new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))),
            _ => (
                "Inconnu",
                (Brush)new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                (Brush)new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))),
        };
        StatusColor.Freeze();
        TextColor.Freeze();
    }

    public override bool Equals(object? obj) =>
        obj is ComPortInfo o && string.Equals(o.Name, Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? "");
}

public enum ComPortStatus { Available, InUse, NotFound, Probing }

public class ActivePosDeviceConfigChangedMessage
{
    public int PosId { get; }
    public ActivePosDeviceConfigChangedMessage(int posId) => PosId = posId;
}

// ════════════════════════════════════════════════════════════════
//  POS MANAGEMENT VIEWMODEL
// ════════════════════════════════════════════════════════════════
public partial class PointOfSaleManagementViewModel : BaseViewModel
{
    private readonly PointOfSaleService _posService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockService _stockService;
    private readonly ITimeProvider _time;
    private readonly FiscalDeviceResolver? _resolver;
    private readonly ILicenseGuard _license;

    public PointOfSaleManagementViewModel(
        PointOfSaleService posService,
        IUnitOfWork unitOfWork,
        StockService stockService,
        ITimeProvider time,
        ILicenseGuard license,
        FiscalDeviceResolver? resolver = null)
    {
        _posService = posService;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _license = license;
        _resolver = resolver;
        PageTitle = "Points de vente";

        _license.StatusChanged += OnLicenseChanged;
        RefreshFeatureGates();

        _ = LoadAsync();
    }

    private void OnLicenseChanged(LicenseSnapshot _)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) RefreshFeatureGates();
        else d.Invoke(RefreshFeatureGates);
    }

    public void Dispose()
    {
        _license.StatusChanged -= OnLicenseChanged;
        GC.SuppressFinalize(this);
    }

    // ── State ──
    [ObservableProperty] private int _companyId;
    [ObservableProperty] private ObservableCollection<PointOfSale> _allPos = new();
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _formTitle = "";
    [ObservableProperty] private bool _isProbingComPorts;

    // ── Licence-driven UI hints ──
    [ObservableProperty] private bool _canAddNewPos = true;
    [ObservableProperty] private string _multiPosLockReason = "";
    [ObservableProperty] private bool _canUseSunmi = true;
    [ObservableProperty] private string _sunmiLockReason = "";
    [ObservableProperty] private bool _canDisableFallback = true;
    [ObservableProperty] private string _fallbackLockReason = "";

    // ── Edit form: General ──
    private int _editId;
    [ObservableProperty] private string _editCode = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editCity = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private bool _editManagesStock = true;
    [ObservableProperty] private bool _editAllowNegativeStock;

    // ── Edit form: Fiscal device ──
    [ObservableProperty] private string _editEmcfUrl = "";
    [ObservableProperty] private string _editEmcfToken = "";
    [ObservableProperty] private string _editEmcfNim = "";

    // ── Edit form: Sunmi terminal (LAN) ──
    [ObservableProperty] private bool _editSunmiEnabled;
    [ObservableProperty] private string _editSunmiTerminalUrl = "";
    [ObservableProperty] private string _editSunmiTerminalId = "";

    [ObservableProperty] private bool _editDisableFallback;

    private bool _editIsEmcfOnly = true;
    public bool EditIsEmcfOnly
    {
        get => _editIsEmcfOnly;
        set
        {
            if (SetProperty(ref _editIsEmcfOnly, value) && value)
            {
                _editIsMcfOnly = false; OnPropertyChanged(nameof(EditIsMcfOnly));
                _editIsHybrid = false; OnPropertyChanged(nameof(EditIsHybrid));
                OnPropertyChanged(nameof(ShowEmcfFields));
                OnPropertyChanged(nameof(ShowMcfFields));
                OnPropertyChanged(nameof(ShowDisableFallbackToggle));
            }
        }
    }

    private bool _editIsMcfOnly;
    public bool EditIsMcfOnly
    {
        get => _editIsMcfOnly;
        set
        {
            if (SetProperty(ref _editIsMcfOnly, value) && value)
            {
                _editIsEmcfOnly = false; OnPropertyChanged(nameof(EditIsEmcfOnly));
                _editIsHybrid = false; OnPropertyChanged(nameof(EditIsHybrid));
                OnPropertyChanged(nameof(ShowEmcfFields));
                OnPropertyChanged(nameof(ShowMcfFields));
                OnPropertyChanged(nameof(ShowDisableFallbackToggle));
                _ = RefreshComPortsAsync(SelectedComPort?.Name);
            }
        }
    }

    private bool _editIsHybrid;
    public bool EditIsHybrid
    {
        get => _editIsHybrid;
        set
        {
            if (SetProperty(ref _editIsHybrid, value) && value)
            {
                _editIsEmcfOnly = false; OnPropertyChanged(nameof(EditIsEmcfOnly));
                _editIsMcfOnly = false; OnPropertyChanged(nameof(EditIsMcfOnly));
                OnPropertyChanged(nameof(ShowEmcfFields));
                OnPropertyChanged(nameof(ShowMcfFields));
                OnPropertyChanged(nameof(ShowDisableFallbackToggle));
                _ = RefreshComPortsAsync(SelectedComPort?.Name);
            }
        }
    }

    public bool ShowEmcfFields => EditIsEmcfOnly || EditIsHybrid;
    public bool ShowMcfFields => EditIsMcfOnly || EditIsHybrid;
    public bool ShowDisableFallbackToggle => EditIsMcfOnly;

    // ── Edit form: MCF serial ──
    [ObservableProperty] private int _editMcfBaudRate = 115200;

    public ObservableCollection<ComPortInfo> AvailableComPorts { get; } = new();
    public int[] AvailableBaudRates { get; } = { 9600, 19200, 38400, 57600, 115200, 230400 };

    private ComPortInfo? _selectedComPort;
    public ComPortInfo? SelectedComPort
    {
        get => _selectedComPort;
        set => SetProperty(ref _selectedComPort, value);
    }

    private void RefreshFeatureGates()
    {
        // Sunmi terminal feature
        CanUseSunmi = _license.TryUse(Feature.SunmiTerminal, out var sReason);
        SunmiLockReason = sReason ?? "";
        if (!CanUseSunmi && EditSunmiEnabled) EditSunmiEnabled = false;

        // e-MCF ↔ MCF fallback feature — required to *disable* fallback on a hybrid box
        CanDisableFallback = _license.TryUse(Feature.EmcfFallback, out var fReason);
        FallbackLockReason = fReason ?? "";
        if (!CanDisableFallback && EditDisableFallback) EditDisableFallback = false;

        // Multi-POS: enforced by count vs. MaxPointsOfSale in claims.
        RecomputeCanAddNewPos();
    }

    private void RecomputeCanAddNewPos()
    {
        var claims = _license.Current.Claims;
        var activeCount = AllPos?.Count(p => p.IsActive) ?? 0;

        // If no claims yet (trial/unknown), allow one POS to unblock first-run.
        var max = claims?.MaxPointsOfSale ?? 1;

        if (activeCount < max)
        {
            CanAddNewPos = true;
            MultiPosLockReason = "";
        }
        else
        {
            CanAddNewPos = false;
            MultiPosLockReason = claims is null
                ? "Aucune licence active — un seul point de vente autorisé."
                : $"Votre licence autorise {max} point(s) de vente actif(s). " +
                  "Passez à une édition Multi-POS pour en ajouter davantage.";
        }
    }

    private string EditMcfPortName => SelectedComPort?.Name ?? "";

    // ── Edit form: Inline test result ──
    [ObservableProperty] private bool _hasEditTestResult;
    [ObservableProperty] private bool _editTestSuccess;
    [ObservableProperty] private string _editTestMessage = "";

    // ── Edit form: Printer ──
    [ObservableProperty] private string _editPrinterName = "";
    [ObservableProperty] private int _editPaperWidth = 80;
    [ObservableProperty] private bool _editAutoPrint = true;
    [ObservableProperty] private int _editPrintCopies = 1;
    [ObservableProperty] private bool _editEnableCustomerDisplay;
    [ObservableProperty] private bool _editEnableCashDrawer;
    [ObservableProperty] private int _editCashDrawerPin;
    [ObservableProperty] private int _editCodePage = 858;
    [ObservableProperty] private bool _editPrintLogo;
    [ObservableProperty] private string _editFooterText = "Merci pour votre achat !";

    public int[] PaperWidths { get; } = { 80, 58 };
    public int[] PrintCopiesOptions { get; } = { 1, 2, 3 };
    public int[] CashDrawerPins { get; } = { 0, 1 };
    public int[] CodePages { get; } = { 858, 850, 437, 1252, 65001 };
    public ObservableCollection<string> DetectedPrinters { get; } = new();

    // ════════════════════════════════════════════════════════════
    //  CRUD
    // ════════════════════════════════════════════════════════════
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (CompanyId == 0)
        {
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            if (company != null) CompanyId = company.Id;
        }

        if (!await EnsureCompanyLoadedAsync()) return;

        var posList = await _posService.GetAllAsync(CompanyId);
        AllPos = new ObservableCollection<PointOfSale>(posList);

        RecomputeCanAddNewPos();
    }

    [RelayCommand]
    private async Task StartNewPosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;

        // Licence: MultiPos slot check.
        RecomputeCanAddNewPos();
        if (!CanAddNewPos)
        {
            ShowErrorMessage(MultiPosLockReason);
            return;
        }

        _editId = 0;
        EditCode = await _posService.GenerateNextCodeAsync(CompanyId);
        EditName = "";
        EditAddress = "";
        EditCity = "";
        EditPhone = "";
        EditManagesStock = true;
        EditAllowNegativeStock = false;

        EditIsEmcfOnly = true;
        EditEmcfUrl = "";
        EditEmcfToken = "";
        EditEmcfNim = "";

        EditMcfBaudRate = 115200;
        EditDisableFallback = false;

        EditSunmiEnabled = false;
        EditSunmiTerminalUrl = "";
        EditSunmiTerminalId = "";

        EditPrinterName = "";
        EditPaperWidth = 80;
        EditAutoPrint = true;
        EditPrintCopies = 1;
        EditEnableCustomerDisplay = false;
        EditEnableCashDrawer = false;
        EditCashDrawerPin = 0;
        EditCodePage = 858;
        EditPrintLogo = false;
        EditFooterText = "Merci pour votre achat !";

        HasEditTestResult = false;
        FormTitle = "Nouveau point de vente";
        IsEditing = true;

        _ = RefreshComPortsAsync(null);
        _ = RefreshPrinterListAsync();
    }

    [RelayCommand]
    private void EditPos(PointOfSale pos)
    {
        _editId = pos.Id;
        EditCode = pos.Code;
        EditName = pos.Name;
        EditAddress = pos.Address;
        EditCity = pos.City;
        EditPhone = pos.Phone;
        EditManagesStock = pos.ManagesStock;
        EditAllowNegativeStock = pos.AllowNegativeStock;

        switch (pos.DeviceType)
        {
            case DeviceType.EMcf: EditIsEmcfOnly = true; break;
            case DeviceType.Mcf: EditIsMcfOnly = true; break;
            case DeviceType.Hybrid: EditIsHybrid = true; break;
        }

        EditEmcfUrl = pos.EmcfApiUrl ?? "";
        EditEmcfToken = pos.EmcfToken ?? "";
        EditEmcfNim = pos.EmcfNIM ?? "";

        EditMcfBaudRate = pos.McfBaudRate > 0 ? pos.McfBaudRate : 115200;
        EditDisableFallback = pos.DisableFallback;

        EditSunmiEnabled = pos.SunmiEnabled;
        EditSunmiTerminalUrl = pos.SunmiTerminalUrl ?? "";
        EditSunmiTerminalId = pos.SunmiTerminalId ?? "";

        EditPrinterName = pos.ThermalPrinterName ?? "";
        EditPaperWidth = pos.PaperWidthMm > 0 ? pos.PaperWidthMm : 80;
        EditAutoPrint = pos.AutoPrintReceipt;
        EditPrintCopies = pos.PrintCopies > 0 ? pos.PrintCopies : 1;
        EditEnableCustomerDisplay = pos.EnableCustomerDisplay;
        EditEnableCashDrawer = pos.EnableCashDrawer;
        EditCashDrawerPin = pos.CashDrawerPin;
        EditCodePage = pos.PrinterCodePage > 0 ? pos.PrinterCodePage : 858;
        EditPrintLogo = pos.PrintLogo;
        EditFooterText = pos.ReceiptFooterText ?? "Merci pour votre achat !";

        HasEditTestResult = false;
        FormTitle = $"Modifier {pos.Code}";
        IsEditing = true;

        _ = RefreshComPortsAsync(pos.McfPortName);
        _ = RefreshPrinterListAsync(EditPrinterName);
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SavePosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;

        var deviceType = EditIsHybrid ? DeviceType.Hybrid
                       : EditIsEmcfOnly ? DeviceType.EMcf
                                        : DeviceType.Mcf;

        if (deviceType is DeviceType.Mcf or DeviceType.Hybrid
            && string.IsNullOrWhiteSpace(EditMcfPortName))
        {
            ShowErrorMessage("Veuillez sélectionner un port COM pour le mode MCF/Hybride.");
            return;
        }

        // ── Licence enforcement ──

        // 1) Sunmi terminal
        if (EditSunmiEnabled && !_license.TryUse(Feature.SunmiTerminal, out var sunmiReason))
        {
            ShowErrorMessage(sunmiReason ?? "Terminal Sunmi non inclus dans votre licence.");
            return;
        }

        // 2) MCF ↔ e-MCF fallback: disabling fallback requires the feature.
        //    (Enabling fallback — the default — is always allowed.)
        if (deviceType == DeviceType.Mcf && EditDisableFallback
            && !_license.TryUse(Feature.EmcfFallback, out var fbReason))
        {
            ShowErrorMessage(fbReason ?? "La désactivation du repli e-MCF requiert la fonctionnalité correspondante.");
            return;
        }

        // 3) MultiPos slot: enforced only on CREATE. Existing POS can always be edited/reactivated.
        if (_editId == 0)
        {
            RecomputeCanAddNewPos();
            if (!CanAddNewPos)
            {
                ShowErrorMessage(MultiPosLockReason);
                return;
            }
        }

        var disableFallback = (deviceType == DeviceType.Mcf) && EditDisableFallback;

        PosSaveResult result;
        int savedPosId; // 🆕 declared outside, will hold the id regardless of branch

        if (_editId == 0)
        {
            var pos = new PointOfSale
            {
                CompanyId = CompanyId,
                Code = EditCode,
                Name = EditName,
                Address = EditAddress,
                City = EditCity,
                Phone = EditPhone,
                ManagesStock = EditManagesStock,
                AllowNegativeStock = EditAllowNegativeStock,
                DeviceType = deviceType,

                EmcfApiUrl = NullIfEmpty(EditEmcfUrl),
                EmcfToken = NullIfEmpty(EditEmcfToken),
                EmcfNIM = NullIfEmpty(EditEmcfNim),

                McfPortName = NullIfEmpty(EditMcfPortName),
                McfBaudRate = EditMcfBaudRate,
                DisableFallback = disableFallback,

                SunmiEnabled = EditSunmiEnabled,
                SunmiTerminalUrl = EditSunmiTerminalUrl?.Trim() ?? "",
                SunmiTerminalId = EditSunmiTerminalId?.Trim() ?? "",

                ThermalPrinterName = EditPrinterName?.Trim() ?? "",
                PaperWidthMm = EditPaperWidth,
                AutoPrintReceipt = EditAutoPrint,
                PrintCopies = EditPrintCopies,
                EnableCustomerDisplay = EditEnableCustomerDisplay,
                EnableCashDrawer = EditEnableCashDrawer,
                CashDrawerPin = EditCashDrawerPin,
                PrinterCodePage = EditCodePage,
                PrintLogo = EditPrintLogo,
                ReceiptFooterText = EditFooterText?.Trim() ?? "Merci pour votre achat !"
            };
            result = await _posService.CreateAsync(pos);
            savedPosId = pos.Id; // ✅ still in scope here
        }
        else
        {
            var pos = await _posService.GetByIdAsync(_editId);
            if (pos == null) { ShowErrorMessage("POS introuvable."); return; }

            pos.Code = EditCode;
            pos.Name = EditName;
            pos.Address = EditAddress;
            pos.City = EditCity;
            pos.Phone = EditPhone;
            pos.ManagesStock = EditManagesStock;
            pos.AllowNegativeStock = EditAllowNegativeStock;
            pos.DeviceType = deviceType;

            pos.EmcfApiUrl = NullIfEmpty(EditEmcfUrl);
            pos.EmcfToken = NullIfEmpty(EditEmcfToken);
            pos.EmcfNIM = NullIfEmpty(EditEmcfNim);

            pos.McfPortName = NullIfEmpty(EditMcfPortName);
            pos.McfBaudRate = EditMcfBaudRate;
            pos.DisableFallback = disableFallback;

            pos.SunmiEnabled = EditSunmiEnabled;
            pos.SunmiTerminalUrl = EditSunmiTerminalUrl?.Trim() ?? "";
            pos.SunmiTerminalId = EditSunmiTerminalId?.Trim() ?? "";

            pos.ThermalPrinterName = EditPrinterName?.Trim() ?? "";
            pos.PaperWidthMm = EditPaperWidth;
            pos.AutoPrintReceipt = EditAutoPrint;
            pos.PrintCopies = EditPrintCopies;
            pos.EnableCustomerDisplay = EditEnableCustomerDisplay;
            pos.EnableCashDrawer = EditEnableCashDrawer;
            pos.CashDrawerPin = EditCashDrawerPin;
            pos.PrinterCodePage = EditCodePage;
            pos.PrintLogo = EditPrintLogo;
            pos.ReceiptFooterText = EditFooterText?.Trim() ?? "Merci pour votre achat !";

            result = await _posService.UpdateAsync(pos);
            savedPosId = pos.Id; // ✅ still in scope here
        }

        if (result.Success)
        {
            try { _resolver?.Invalidate(); } catch { /* non-fatal */ }

            WeakReferenceMessenger.Default.Send(new ActivePosDeviceConfigChangedMessage(savedPosId)); // ✅ use the captured id

            IsEditing = false;
            await LoadAsync();
            _ = ShowSuccessAsync(_editId == 0 ? "✅ POS créé avec succès." : "✅ POS mis à jour.");

        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task DeactivatePosAsync(PointOfSale pos)
    {
        var result = await _posService.DeactivateAsync(pos.Id);
        if (result.Success)
        {
            await LoadAsync();
            _ = ShowSuccessAsync($"POS {pos.Code} désactivé.");
        }
        else ShowErrorMessage(result.ErrorMessage);
    }

    [RelayCommand]
    private async Task ReactivatePosAsync(PointOfSale? pos)
    {
        if (pos == null || pos.IsActive) return;

        // Réactiver augmente le nombre de POS actifs → même limite que la création.
        RecomputeCanAddNewPos();
        if (!CanAddNewPos)
        {
            ShowErrorMessage(MultiPosLockReason);
            return;
        }

        var result = await _posService.ReactivateAsync(pos.Id);
        if (result.Success)
        {
            try { _resolver?.Invalidate(); } catch { /* non-fatal */ }
            await LoadAsync();
            _ = ShowSuccessAsync($"✅ POS {pos.Code} réactivé.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }


    [RelayCommand]
    private async Task InitializeStockAsync(PointOfSale pos)
    {
        IsBusy = true;
        try
        {
            var count = await _stockService.InitializePosStockFromProductsAsync(pos.Id, "Admin");
            _ = ShowSuccessAsync($"✅ {count} produit(s) initialisé(s) dans {pos.Code}.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur initialisation stock : {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ════════════════════════════════════════════════════════════
    //  COM PORT DETECTION
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private Task RefreshComPorts() => RefreshComPortsAsync(SelectedComPort?.Name);

    private async Task RefreshComPortsAsync(string? preferredPort)
    {
        if (IsProbingComPorts) return;
        IsProbingComPorts = true;
        try
        {
            string[] systemPorts;
            try { systemPorts = SerialPort.GetPortNames().Distinct().OrderBy(p => p).ToArray(); }
            catch { systemPorts = Array.Empty<string>(); }

            AvailableComPorts.Clear();
            foreach (var p in systemPorts)
                AvailableComPorts.Add(new ComPortInfo(p, ComPortStatus.Probing));

            if (!string.IsNullOrWhiteSpace(preferredPort)
                && !AvailableComPorts.Any(p => string.Equals(p.Name, preferredPort, StringComparison.OrdinalIgnoreCase)))
            {
                AvailableComPorts.Add(new ComPortInfo(preferredPort, ComPortStatus.NotFound));
            }

            SelectedComPort =
                (!string.IsNullOrWhiteSpace(preferredPort)
                    ? AvailableComPorts.FirstOrDefault(p =>
                          string.Equals(p.Name, preferredPort, StringComparison.OrdinalIgnoreCase))
                    : null)
                ?? AvailableComPorts.FirstOrDefault();

            if (systemPorts.Length == 0) return;

            // 🆕 If the resolver currently owns a port, do NOT probe it — just mark it InUse.
            string? activePosPort = null;
            if (_resolver != null)
            {
                try
                {
                    var diag = await _resolver.GetDiagnosticsAsync();
                    activePosPort = diag.ConfiguredPortName;
                }
                catch { /* ignore */ }
            }

            var portsToProbe = systemPorts
                .Where(p => !string.Equals(p, activePosPort, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var probed = await Task.Run(() =>
                portsToProbe
                    .AsParallel()
                    .WithDegreeOfParallelism(Math.Max(2, Math.Min(portsToProbe.Length, 8)))
                    .Select(p => (Name: p, Status: ProbePort(p)))
                    .ToList());

            // Mark the resolver-owned port as InUse without touching it.
            if (!string.IsNullOrEmpty(activePosPort)
                && systemPorts.Contains(activePosPort, StringComparer.OrdinalIgnoreCase))
            {
                probed.Add((activePosPort!, ComPortStatus.InUse));
            }

            foreach (var (name, status) in probed)
            {
                var idx = -1;
                for (int i = 0; i < AvailableComPorts.Count; i++)
                    if (string.Equals(AvailableComPorts[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    { idx = i; break; }

                if (idx >= 0)
                    AvailableComPorts[idx] = new ComPortInfo(name, status);
            }

            if (SelectedComPort != null)
            {
                var keep = SelectedComPort.Name;
                SelectedComPort = AvailableComPorts.FirstOrDefault(p =>
                    string.Equals(p.Name, keep, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally { IsProbingComPorts = false; }
    }

    private static ComPortStatus ProbePort(string portName)
    {
        try
        {
            using var sp = new SerialPort(portName) { ReadTimeout = 50, WriteTimeout = 50 };
            sp.Open();
            // Hold briefly so a concurrent legitimate user surfaces as InUse.
            Thread.Sleep(20);
            sp.Close();
            return ComPortStatus.Available;
        }
        catch (UnauthorizedAccessException) { return ComPortStatus.InUse; }
        catch (IOException) { return ComPortStatus.NotFound; }
        catch { return ComPortStatus.InUse; }
    }

    // ════════════════════════════════════════════════════════════
    //  INLINE MCF / e-MCF TEST
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task TestEditingPosConnectionAsync()
    {
        if (IsProbingComPorts)
        {
            EditTestSuccess = false;
            EditTestMessage = "Analyse des ports en cours, veuillez patienter…";
            HasEditTestResult = true;
            return;
        }

        HasEditTestResult = false;
        IsBusy = true;

        // 🆕 Take exclusive ownership of any port the resolver might be holding.
        IDisposable? lease = null;
        if (_resolver != null)
        {
            try { lease = await _resolver.AcquireExclusiveAccessAsync(); }
            catch { /* non-fatal */ }
        }

        IFiscalDeviceService? device = null;
        McfSerialClient? mcfOwned = null;

        try
        {
            if (EditIsMcfOnly || EditIsHybrid)
            {
                if (string.IsNullOrWhiteSpace(EditMcfPortName))
                {
                    EditTestSuccess = false;
                    EditTestMessage = "Aucun port COM sélectionné.";
                    HasEditTestResult = true;
                    return;
                }

                if (SelectedComPort?.Status == ComPortStatus.NotFound)
                {
                    EditTestSuccess = false;
                    EditTestMessage = $"Le port {EditMcfPortName} n'est pas disponible sur cette machine.";
                    HasEditTestResult = true;
                    return;
                }

                mcfOwned = new McfSerialClient(EditMcfPortName, _time, EditMcfBaudRate);
                mcfOwned.Connect();
                device = mcfOwned;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(EditEmcfUrl) || string.IsNullOrWhiteSpace(EditEmcfToken))
                {
                    EditTestSuccess = false;
                    EditTestMessage = "URL ou token e-MCF manquant.";
                    HasEditTestResult = true;
                    return;
                }
                device = new EMcfHttpClient(EditEmcfUrl, EditEmcfToken, "", _time);
            }

            var status = await device.GetStatusAsync();

            EditTestSuccess = status.Success;
            EditTestMessage = status.Success
                ? $"Connexion réussie — NIM : {status.NIM}"
                : $"Échec : {status.ErrorMessage}";
            HasEditTestResult = true;
        }
        catch (Exception ex)
        {
            EditTestSuccess = false;
            EditTestMessage = $"Erreur : {ex.GetBaseException().Message}";
            HasEditTestResult = true;
        }
        finally
        {
            // Dispose mcfOwned if it was created (covers the Connect-throws case);
            // otherwise dispose the e-MCF client through IDisposable.
            if (mcfOwned != null)
            {
                try { mcfOwned.Dispose(); } catch { }
            }
            else if (device is IDisposable d)
            {
                try { d.Dispose(); } catch { }
            }

            IsBusy = false;
            lease?.Dispose();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  PRINTER
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private Task RefreshPrinterList() => RefreshPrinterListAsync();

    private async Task RefreshPrinterListAsync(string? preferred = null)
    {
        var previous = preferred ?? EditPrinterName;
        var found = await Task.Run(() =>
        {
            var list = new List<string> { "" };
            try
            {
                foreach (string p in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                    list.Add(p);
            }
            catch { }
            return list;
        });

        DetectedPrinters.Clear();
        foreach (var p in found) DetectedPrinters.Add(p);

        if (!string.IsNullOrEmpty(previous) && DetectedPrinters.Contains(previous))
            EditPrinterName = previous;
    }

    [RelayCommand]
    private async Task AutoDetectPrinterAsync()
    {
        string[] thermalKeywords =
        {
            "pos", "thermal", "receipt", "epson", "tm-t", "tm-m",
            "star ", "tsp", "bixolon", "srp-", "citizen", "ct-",
            "xprinter", "xp-", "rongta", "rp-", "zjiang",
            "pos-58", "pos-80", "80mm", "58mm", "optima"
        };

        var found = await Task.Run(() =>
        {
            try
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    string lower = printer.ToLowerInvariant();
                    foreach (var kw in thermalKeywords)
                        if (lower.Contains(kw)) return printer;
                }
            }
            catch { }
            return null!;
        });

        if (!string.IsNullOrEmpty(found))
        {
            EditPrinterName = found;
            _ = ShowSuccessAsync($"🖨 Imprimante détectée : {found}");
        }
        else
        {
            ShowErrorMessage("Aucune imprimante thermique détectée automatiquement.");
        }
    }

    [RelayCommand]
    private async Task TestPrintAsync()
    {
        string printerName = EditPrinterName?.Trim() ?? "";
        if (string.IsNullOrEmpty(printerName))
        {
            await AutoDetectPrinterAsync();
            printerName = EditPrinterName?.Trim() ?? "";
        }
        if (string.IsNullOrEmpty(printerName))
        {
            ShowErrorMessage("Aucune imprimante configurée ou détectée.");
            return;
        }

        try
        {
            int charsPerLine = EditPaperWidth >= 80 ? 48 : 32;
            byte[] receipt = BuildTestReceipt(charsPerLine, EditCodePage, _time.LocalNow);
            await Task.Run(() => RawPrinterHelper.SendBytesToPrinter(printerName, receipt, "SFE-TestPrint"));
            _ = ShowSuccessAsync($"🖨 Ticket test envoyé à « {printerName} ».");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur impression : {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════

    private async Task<bool> EnsureCompanyLoadedAsync()
    {
        if (CompanyId > 0) return true;
        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company != null) { CompanyId = company.Id; return true; }
        ShowErrorMessage("Aucune entreprise configurée.");
        return false;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static byte[] BuildTestReceipt(int charsPerLine, int codePage, DateTimeOffset now)
    {
        using var ms = new MemoryStream();
        void Write(params byte[] data) => ms.Write(data, 0, data.Length);

        Encoding enc;
        try { enc = Encoding.GetEncoding(codePage); }
        catch { enc = Encoding.GetEncoding(437); }

        void PrintLine(string text)
        {
            byte[] bytes = enc.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
            ms.WriteByte(0x0A);
        }

        Write(0x1B, 0x40);
        byte cpByte = codePage switch { 437 => 0x00, 850 => 0x02, 858 => 0x13, 1252 => 0x10, _ => 0x13 };
        Write(0x1B, 0x74, cpByte);
        Write(0x1B, 0x61, 0x01);
        Write(0x1B, 0x45, 0x01);
        Write(0x1D, 0x21, 0x11);
        PrintLine("SFE GECOM");
        Write(0x1D, 0x21, 0x00);
        Write(0x1B, 0x45, 0x00);
        PrintLine("");
        PrintLine(new string('=', charsPerLine));
        PrintLine("TEST D'IMPRESSION");
        PrintLine(new string('=', charsPerLine));
        PrintLine("");
        PrintLine($"Largeur : {(charsPerLine >= 48 ? 80 : 58)} mm");
        PrintLine($"Caractères/ligne : {charsPerLine}");
        PrintLine($"Code page : {codePage}");
        PrintLine($"Date : {now:dd/MM/yyyy HH:mm:ss}");
        PrintLine("");
        PrintLine("Caractères spéciaux :");
        PrintLine("é è ê ë à â ù û ç ô î ï €");
        PrintLine("");
        PrintLine(new string('-', charsPerLine));
        Write(0x1B, 0x61, 0x00);

        void PrintRow(string left, string right)
        {
            int gap = charsPerLine - left.Length - right.Length;
            if (gap < 1) gap = 1;
            PrintLine(left + new string(' ', gap) + right);
        }
        PrintRow("Article test", "1 500,00 CDF");
        PrintRow("TVA 16%", "240,00 CDF");
        Write(0x1B, 0x61, 0x01);
        PrintLine(new string('-', charsPerLine));
        Write(0x1B, 0x61, 0x00);
        Write(0x1B, 0x45, 0x01);
        Write(0x1D, 0x21, 0x01);
        PrintRow("TOTAL TTC", "1 740,00 CDF");
        Write(0x1D, 0x21, 0x00);
        Write(0x1B, 0x45, 0x00);
        Write(0x1B, 0x61, 0x01);
        PrintLine(new string('=', charsPerLine));
        PrintLine("");
        PrintLine("Si ce ticket s'imprime");
        PrintLine("correctement, votre");
        PrintLine("imprimante est configurée !");
        PrintLine("");
        PrintLine("--- iKWOOK SFE ---");
        PrintLine("");
        Write(0x1B, 0x64, 0x05);
        Write(0x1D, 0x56, 0x01);
        return ms.ToArray();
    }

    [RelayCommand]
    private async Task TestPosConnection(PointOfSale? pos)
    {
        if (pos == null) return;
        IsBusy = true;

        // 🆕 Lease the port before opening it directly.
        IDisposable? lease = null;
        if (_resolver != null)
        {
            try { lease = await _resolver.AcquireExclusiveAccessAsync(); }
            catch { /* non-fatal */ }
        }

        IFiscalDeviceService? device = null;
        McfSerialClient? mcfOwned = null;

        try
        {
            if (pos.DeviceType == DeviceType.EMcf || pos.DeviceType == DeviceType.Hybrid)
            {
                device = new EMcfHttpClient(
                    pos.EmcfApiUrl ?? "",
                    pos.EmcfToken ?? "",
                    "",
                    _time);
            }
            else
            {
                mcfOwned = new McfSerialClient(pos.McfPortName ?? "COM3", _time, pos.McfBaudRate);
                mcfOwned.Connect();
                device = mcfOwned;
            }

            var status = await device.GetStatusAsync();

            if (status.Success)
            {
                pos.LastKnownNIM = status.NIM;
                pos.LastKnownNIF = status.NIF;
                pos.LastConnectionTestAt = _time.UtcNow.UtcDateTime;

                if (!string.IsNullOrEmpty(status.NIM))
                    pos.EmcfNIM = status.NIM;

                var serverStatus = await device.GetServerConnectionStatusAsync();
                if (serverStatus.Success)
                {
                    pos.McfLastServerConnection = serverStatus.LastServerConnection;
                    pos.McfServerStatus = serverStatus.ConnectionStatus;
                }

                await _posService.UpdateAsync(pos);
                await LoadAsync();
                _ = ShowSuccessAsync($"✅ Connexion réussie — NIM: {status.NIM}");
            }
            else
            {
                ShowErrorMessage($"Échec: {status.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur: {ex.Message}");
        }
        finally
        {
            if (mcfOwned != null)
            {
                try { mcfOwned.Dispose(); } catch { }
            }
            else if (device is IDisposable d)
            {
                try { d.Dispose(); } catch { }
            }

            IsBusy = false;
            lease?.Dispose();
        }
    }
}