using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using SFE.WPF.Messages;
using SFE.WPF.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SFE.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly ITimeProvider _time;
    private int _companyId;
    private int _activePosId;
    private bool _isLoading;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ══════════ ENTREPRISE ══════════
    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _companyNIF = "";
    [ObservableProperty] private string _companyRCCM = "";
    [ObservableProperty] private string _companyAddress = "";
    [ObservableProperty] private string _companyCity = "";
    [ObservableProperty] private string _companyPhone = "";
    [ObservableProperty] private string _companyEmail = "";
    [ObservableProperty] private string _companyISF = "";

    // ══════════ LOGO ══════════
    [ObservableProperty] private ImageSource? _companyLogoPreview;
    [ObservableProperty] private bool _hasLogo;
    private byte[]? _companyLogoBytes;

    // ══════════ DISPOSITIF FISCAL ══════════
    [ObservableProperty] private bool _isEmcfSelected = true;
    [ObservableProperty] private string _emcfApiUrl = "";
    [ObservableProperty] private string _emcfToken = "";
    [ObservableProperty] private string _emcfNIM = "";
    [ObservableProperty] private string _selectedComPort = "";
    [ObservableProperty] private int _baudRate = 115200;

    [ObservableProperty] private string _activePosDeviceSummary = "—";
    [ObservableProperty] private string _activePosCode = "";
    [ObservableProperty] private string _activePosLabel = "—";

    // ══════════ COM PORTS ══════════
    public ObservableCollection<string> AvailableComPorts { get; } = new();
    public int[] AvailableBaudRates { get; } = { 9600, 19200, 38400, 57600, 115200 };

    // ══════════ MODE DE PRIX ══════════
    [ObservableProperty] private bool _isPriceModeTTC = true;

    // ══════════ REMISE ══════════
    [ObservableProperty] private bool _discountBeforeTax = true;

    // ══════════ ISF VISIBILITY ══════════
    private readonly IAuthService _authService;
    public bool CanSeeISF => _authService.IsInRole("SuperAdmin", "IT Tech");

    // ══════════ DEVISE ══════════
    [ObservableProperty] private Currency _defaultCurrency = Currency.CDF;
    [ObservableProperty] private string _currentExchangeRate = "2800";
    [ObservableProperty] private string _currentExchangeRateEUR = "3100";
    [ObservableProperty] private string _currentExchangeRateCNY = "385";
    [ObservableProperty] private ExchangeRateMode _exchangeRateMode = ExchangeRateMode.Manual;

    public Currency[] Currencies { get; } = Enum.GetValues<Currency>();
    public ExchangeRateMode[] ExchangeRateModes { get; } = Enum.GetValues<ExchangeRateMode>();

    // ══════════ DGI RATE FETCH ══════════
    [ObservableProperty] private bool _isFetchingDgiRates;
    [ObservableProperty] private string _dgiRateStatus = "";
    [ObservableProperty] private bool _showDgiRateSuccess;
    [ObservableProperty] private bool _showDgiRateError;
    [ObservableProperty] private bool _hasDgiRate;
    [ObservableProperty] private string _dgiUsdRate = "—";
    [ObservableProperty] private string _dgiUsdDate = "—";
    private DateTimeOffset? _dgiExchangeRateDate;

    // ══════════ FIDÉLITÉ ══════════
    [ObservableProperty] private bool _isLoyaltyEnabled;
    [ObservableProperty] private string _loyaltyEarnRate = "1000";
    [ObservableProperty] private string _loyaltyRedeemRate = "500";
    [ObservableProperty] private string _loyaltyMinRedeemPoints = "100";

    // ══════════ SAVE STATUS ══════════
    [ObservableProperty] private string _saveStatus = "";
    [ObservableProperty] private bool _showSaveSuccess;
    [ObservableProperty] private bool _showSaveError;
    [ObservableProperty] private bool _isLoaded;

    // ══════════ TEST CONNEXION ══════════
    [ObservableProperty] private bool _isTestingConnection;
    [ObservableProperty] private bool _hasTestResult;
    [ObservableProperty] private bool _testSuccess;
    [ObservableProperty] private string _testStatus = "";
    [ObservableProperty] private string _testMessage = "";
    [ObservableProperty] private string _testResponseTime = "";
    [ObservableProperty] private string _testRawResponse = "";
    [ObservableProperty] private string _testDetails = "";
    [ObservableProperty] private string _testNIM = "";
    [ObservableProperty] private string _testServerVersion = "";

    // ══════════ DETAILED DEVICE INFO ══════════
    [ObservableProperty] private bool _isDeviceInfoLoading;
    [ObservableProperty] private bool _hasDeviceInfo;
    [ObservableProperty] private bool _deviceInfoSuccess;
    [ObservableProperty] private string _deviceInfoError = "";

    [ObservableProperty] private string _deviceInfoType = "—";
    [ObservableProperty] private string _deviceNIM = "—";
    [ObservableProperty] private string _deviceNIF = "—";
    [ObservableProperty] private string _deviceConnectionStatus = "DIS";
    [ObservableProperty] private string _deviceConnectionLabel = "Déconnecté";
    [ObservableProperty] private string _deviceLastSync = "—";
    [ObservableProperty] private string _deviceDateTime = "—";
    [ObservableProperty] private string _deviceTaxpayerName = "—";
    [ObservableProperty] private string _deviceTaxpayerAddress = "—";
    [ObservableProperty] private string _deviceTaxpayerCity = "—";
    [ObservableProperty] private string _deviceTaxpayerPhone = "—";
    [ObservableProperty] private string _deviceTaxpayerEmail = "—";
    [ObservableProperty] private string _deviceTotalTransactions = "0";
    [ObservableProperty] private string _deviceSalesCount = "0";
    [ObservableProperty] private string _deviceCreditNoteCount = "0";
    [ObservableProperty] private string _deviceTransactionsSent = "0";
    [ObservableProperty] private string _deviceTransactionsInDevice = "0";
    [ObservableProperty] private string _devicePendingCount = "0";
    [ObservableProperty] private string _deviceLastInvoice = "—";
    [ObservableProperty] private string _deviceTokenValid = "—";
    [ObservableProperty] private string _deviceApiVersion = "—";
    [ObservableProperty] private string _deviceEmcfStatus = "—";
    [ObservableProperty] private string _deviceLastError = "—";
    [ObservableProperty] private string _deviceTaxRatesDisplay = "";

    // ══════════ HEALTH REPORT (synthetic verdict) ══════════
    [ObservableProperty] private bool _hasHealthReport;
    [ObservableProperty] private string _deviceHealthStatus = "Unknown";   // Healthy / Degraded / Unhealthy / Unknown
    [ObservableProperty] private string _deviceHealthLabel = "—";          // Localized label for the badge
    [ObservableProperty] private string _deviceHealthSummary = "";         // One-line summary
    [ObservableProperty] private string _deviceHealthBadgeKind = "neutral";// "ok" | "warn" | "error" | "neutral"
    [ObservableProperty] private string _deviceHealthSyncAge = "—";        // Pre-formatted "il y a 12 min"
    [ObservableProperty]
    private ObservableCollection<string> _deviceHealthWarnings = new();

    [ObservableProperty]
    private ObservableCollection<CurrencyRateDisplayItem> _deviceCurrencyRates = new();

    [ObservableProperty]
    private ObservableCollection<EmcfDeviceDisplayItem> _deviceEmcfList = new();

    // ══════════ LICENCE ══════════
    [ObservableProperty] private string _licenseKey = "";
    [ObservableProperty] private string _licenseStatus = "Non activée";
    [ObservableProperty] private string _licensePlan = "Free";
    [ObservableProperty] private string _licenseMessage = "";

    // ══════════════════════════════════════════════════════════════
    // CONSTRUCTEUR
    // ══════════════════════════════════════════════════════════════

    public SettingsViewModel(
        SettingsService settingsService,
        IAuthService authService,
        IFiscalDeviceService fiscalDevice,
        ITimeProvider time)
    {
        _settingsService = settingsService;
        _authService = authService;
        _fiscalDevice = fiscalDevice;
        _time = time;
        PageTitle = "Paramètres";

        RefreshComPorts();
        _ = LoadSettingsAsync();
    }

    // ══════════════════════════════════════════════════════════════
    // LOGO COMMANDS
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void BrowseLogo()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choisir un logo",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|Tous les fichiers|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _companyLogoBytes = File.ReadAllBytes(dlg.FileName);
                CompanyLogoPreview = BytesToImage(_companyLogoBytes);
                HasLogo = true;
            }
            catch (Exception ex)
            {
                SaveStatus = $"Erreur logo : {ex.Message}";
                ShowSaveError = true;
            }
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        _companyLogoBytes = null;
        CompanyLogoPreview = null;
        HasLogo = false;
    }

    private static BitmapImage? BytesToImage(byte[]? data)
    {
        if (data == null || data.Length == 0) return null;
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(data);
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ══════════════════════════════════════════════════════════════
    // COM PORTS
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RefreshComPorts()
    {
        var previousSelection = SelectedComPort;
        var ports = SerialPort.GetPortNames();

        AvailableComPorts.Clear();

        if (ports.Length > 0)
        {
            var sorted = ports
                .Distinct()
                .OrderBy(p =>
                {
                    var num = p.Replace("COM", "");
                    return int.TryParse(num, out var n) ? n : 999;
                })
                .ToList();

            foreach (var port in sorted)
                AvailableComPorts.Add(port);
        }
        else
        {
            AvailableComPorts.Add("(aucun port détecté)");
        }

        if (!string.IsNullOrEmpty(previousSelection) && AvailableComPorts.Contains(previousSelection))
            SelectedComPort = previousSelection;
        else
            SelectedComPort = AvailableComPorts.First();
    }

    // ══════════════════════════════════════════════════════════════
    // BROADCASTS
    // ══════════════════════════════════════════════════════════════

    partial void OnIsPriceModeTTCChanged(bool value)
    {
        if (_isLoading) return;
        var newMode = value ? PriceMode.TTC : PriceMode.HT;
        WeakReferenceMessenger.Default.Send(new PriceModeChangedMessage(newMode));
    }

    partial void OnDiscountBeforeTaxChanged(bool value)
    {
        if (_isLoading) return;
        WeakReferenceMessenger.Default.Send(new DiscountBeforeTaxChangedMessage(value));
    }

    private void BroadcastExchangeRates()
    {
        decimal.TryParse(CurrentExchangeRate, NumberStyles.Any, Inv, out var usd);
        decimal.TryParse(CurrentExchangeRateEUR, NumberStyles.Any, Inv, out var eur);
        decimal.TryParse(CurrentExchangeRateCNY, NumberStyles.Any, Inv, out var cny);

        WeakReferenceMessenger.Default.Send(
            new ExchangeRateChangedMessage(
                new ExchangeRatePayload(usd, eur, cny, _dgiExchangeRateDate)));
    }


    // ══════════════════════════════════════════════════════════════
    // LOAD SETTINGS
    // ══════════════════════════════════════════════════════════════
    private async Task LoadSettingsAsync()
    {
        _isLoading = true;
        IsBusy = true;
        try
        {
            var data = await _settingsService.LoadSettingsAsync();
            _companyId = data.CompanyId;
            _activePosId = data.ActivePosId;

            CompanyName = data.CompanyName;
            CompanyNIF = data.CompanyNIF;
            CompanyRCCM = data.CompanyRCCM;
            CompanyAddress = data.CompanyAddress;
            CompanyCity = data.CompanyCity;
            CompanyPhone = data.CompanyPhone;
            CompanyEmail = data.CompanyEmail;
            CompanyISF = data.CompanyISF;

            _companyLogoBytes = data.CompanyLogo;
            CompanyLogoPreview = BytesToImage(_companyLogoBytes);
            HasLogo = _companyLogoBytes is { Length: > 0 };

            ApplyActivePosDeviceConfig(data);

            IsPriceModeTTC = data.DefaultPriceMode == PriceMode.TTC;
            DiscountBeforeTax = data.DiscountBeforeTax;
            DefaultCurrency = data.DefaultCurrency;

            CurrentExchangeRate = data.CurrentExchangeRate.ToString("F2", Inv);
            CurrentExchangeRateEUR = data.CurrentExchangeRateEUR.ToString("F2", Inv);
            CurrentExchangeRateCNY = data.CurrentExchangeRateCNY.ToString("F2", Inv);
            ExchangeRateMode = data.ExchangeRateMode;

            _dgiExchangeRateDate = data.DgiExchangeRateDate;
            if (_dgiExchangeRateDate.HasValue && data.CurrentExchangeRate > 0)
            {
                DgiUsdRate = data.CurrentExchangeRate.ToString("N2", Inv);
                DgiUsdDate = _dgiExchangeRateDate.Value.ToString("dd/MM/yyyy", Inv);
                HasDgiRate = true;
            }

            IsLoyaltyEnabled = data.LoyaltyEnabled;
            LoyaltyEarnRate = data.LoyaltyEarnRate.ToString("0", Inv);
            LoyaltyRedeemRate = data.LoyaltyRedeemRate.ToString("0", Inv);
            LoyaltyMinRedeemPoints = data.LoyaltyMinRedeemPoints.ToString(Inv);

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"Erreur de chargement : {ex.Message}";
            ShowSaveError = true;
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
        }
    }

    private void ApplyActivePosDeviceConfig(SettingsData data)
    {
        IsEmcfSelected = data.DeviceType == DeviceType.EMcf;
        EmcfApiUrl = data.EmcfApiUrl;
        EmcfToken = data.EmcfToken;
        EmcfNIM = data.EmcfNIM;

        if (!string.IsNullOrEmpty(data.McfPortName) && AvailableComPorts.Contains(data.McfPortName))
            SelectedComPort = data.McfPortName;
        else if (AvailableComPorts.Count > 0 && AvailableComPorts[0] != "(aucun port détecté)")
            SelectedComPort = AvailableComPorts.First();
        else
            SelectedComPort = AvailableComPorts.FirstOrDefault() ?? "";

        BaudRate = data.McfBaudRate > 0 ? data.McfBaudRate : 115200;

        ActivePosCode = data.ActivePosCode ?? "";
        ActivePosLabel = string.IsNullOrWhiteSpace(data.ActivePosName)
            ? (data.ActivePosCode ?? "—")
            : $"{data.ActivePosCode} — {data.ActivePosName}";

        ActivePosDeviceSummary = IsEmcfSelected
            ? $"e-MCF · {(string.IsNullOrWhiteSpace(EmcfApiUrl) ? "(URL non configurée)" : EmcfApiUrl)}"
            : $"MCF · {(string.IsNullOrWhiteSpace(SelectedComPort) ? "(port non configuré)" : SelectedComPort)} @ {BaudRate}";
    }

    [RelayCommand]
    private async Task RefreshActivePosDeviceConfig()
    {
        if (_fiscalDevice is FiscalDeviceResolver resolver)
            resolver.Invalidate();
        await LoadSettingsAsync();
    }

    // ══════════════════════════════════════════════════════════════
    // SAVE SETTINGS
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SaveSettings()
    {
        IsBusy = true;
        ShowSaveSuccess = false;
        ShowSaveError = false;
        SaveStatus = "";

        try
        {
            if (!TryParseDecimalStrict(CurrentExchangeRate, out var rateUsd, out var errUsd))
            {
                SaveStatus = $"Taux USD invalide : {errUsd}";
                ShowSaveError = true;
                return;
            }
            if (!TryParseDecimalStrict(CurrentExchangeRateEUR, out var rateEur, out var errEur))
            {
                SaveStatus = $"Taux EUR invalide : {errEur}";
                ShowSaveError = true;
                return;
            }
            if (!TryParseDecimalStrict(CurrentExchangeRateCNY, out var rateCny, out var errCny))
            {
                SaveStatus = $"Taux CNY invalide : {errCny}";
                ShowSaveError = true;
                return;
            }
            if (!TryParseDecimalStrict(LoyaltyEarnRate, out var earn, out var errEarn, allowZero: true))
            {
                SaveStatus = $"Taux de gain fidélité invalide : {errEarn}";
                ShowSaveError = true;
                return;
            }
            if (!TryParseDecimalStrict(LoyaltyRedeemRate, out var redeem, out var errRedeem, allowZero: true))
            {
                SaveStatus = $"Taux de conversion fidélité invalide : {errRedeem}";
                ShowSaveError = true;
                return;
            }
            if (!int.TryParse(LoyaltyMinRedeemPoints, NumberStyles.Integer, Inv, out var minPts)
                || minPts < 0)
            {
                SaveStatus = "Nombre minimum de points invalide.";
                ShowSaveError = true;
                return;
            }

            var existing = await _settingsService.LoadSettingsAsync();

            var data = new SettingsData
            {
                CompanyId = _companyId,
                CompanyName = CompanyName,
                CompanyNIF = CompanyNIF,
                CompanyISF = CompanyISF,
                CompanyRCCM = CompanyRCCM,
                CompanyAddress = CompanyAddress,
                CompanyCity = CompanyCity,
                CompanyPhone = CompanyPhone,
                CompanyEmail = CompanyEmail,
                CompanyLogo = _companyLogoBytes,
                DefaultPriceMode = IsPriceModeTTC ? PriceMode.TTC : PriceMode.HT,
                DiscountBeforeTax = DiscountBeforeTax,
                DefaultCurrency = DefaultCurrency,
                CurrentExchangeRate = rateUsd,
                CurrentExchangeRateEUR = rateEur,
                CurrentExchangeRateCNY = rateCny,
                ExchangeRateMode = ExchangeRateMode,
                DgiExchangeRateDate = _dgiExchangeRateDate,
                LoyaltyEnabled = IsLoyaltyEnabled,
                LoyaltyEarnRate = earn,
                LoyaltyRedeemRate = redeem,
                LoyaltyMinRedeemPoints = minPts,
                DeploymentMode = DeploymentMode.Standalone,
                ActivePosId = _activePosId,
                DeviceType = existing.DeviceType,
                EmcfApiUrl = existing.EmcfApiUrl,
                EmcfToken = existing.EmcfToken,
                EmcfNIM = existing.EmcfNIM,
                McfPortName = existing.McfPortName,
                McfBaudRate = existing.McfBaudRate,
            };

            await _settingsService.SaveSettingsAsync(data);

            if (_fiscalDevice is FiscalDeviceResolver resolver)
                resolver.Invalidate();

            WeakReferenceMessenger.Default.Send(
                new PriceModeChangedMessage(data.DefaultPriceMode));
            WeakReferenceMessenger.Default.Send(
                new DiscountBeforeTaxChangedMessage(data.DiscountBeforeTax));
            BroadcastExchangeRates();

            SaveStatus = "Paramètres enregistrés avec succès !";
            ShowSaveSuccess = true;

            await Task.Delay(4000);
            ShowSaveSuccess = false;
        }
        catch (Exception ex)
        {
            SaveStatus = $"Erreur : {ex.Message}";
            ShowSaveError = true;
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════════════════════
    // FETCH DGI CURRENCY RATES
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task FetchDgiRates()
    {
        IsFetchingDgiRates = true;
        ShowDgiRateSuccess = false;
        ShowDgiRateError = false;
        DgiRateStatus = "";

        try
        {
            var info = await _fiscalDevice.GetDetailedInfoAsync();

            if (!info.Success)
            {
                DgiRateStatus = $"Impossible de contacter le dispositif : {info.ErrorMessage}";
                ShowDgiRateError = true;
                return;
            }

            if (info.CurrencyRates == null || info.CurrencyRates.Count == 0)
            {
                DgiRateStatus = IsEmcfSelected
                    ? "Aucun taux de change retourné par la DGI."
                    : "Le MCF n'a pas retourné de taux de change. Vérifiez que le dispositif est synchronisé avec le serveur DGI.";
                ShowDgiRateError = true;
                return;
            }

            var usdRate = info.CurrencyRates.FirstOrDefault(r =>
                r.Code.Equals("USD", StringComparison.OrdinalIgnoreCase));

            if (usdRate == null || usdRate.Rate <= 0)
            {
                DgiRateStatus = "Taux USD non trouvé dans la réponse DGI.";
                ShowDgiRateError = true;
                return;
            }

            ApplyDgiUsdRateToUi(usdRate.Rate, usdRate.Date);

            DeviceCurrencyRates.Clear();
            foreach (var cr in info.CurrencyRates)
            {
                DeviceCurrencyRates.Add(new CurrencyRateDisplayItem
                {
                    Code = cr.Code ?? "?",
                    Description = cr.Description ?? "",
                    Date = cr.Date != default ? cr.Date.ToString("dd/MM/yyyy", Inv) : "—",
                    Rate = cr.Rate.ToString("N2", Inv)
                });
            }

            await PersistDgiUsdRateAsync(usdRate.Rate, _dgiExchangeRateDate);

            DgiRateStatus = $"✓ Taux USD appliqué : 1 USD = {usdRate.Rate.ToString("N2", Inv)} CDF — Date DGI : {DgiUsdDate}";
            ShowDgiRateSuccess = true;

            await Task.Delay(5000);
            ShowDgiRateSuccess = false;
        }
        catch (Exception ex)
        {
            DgiRateStatus = $"Erreur : {ex.Message}";
            ShowDgiRateError = true;
        }
        finally
        {
            IsFetchingDgiRates = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LOAD DETAILED DEVICE INFO
    // ══════════════════════════════════════════════════════════════

    private CancellationTokenSource? _deviceInfoCts;

    [ObservableProperty] private string _deviceRespondingBadge = "—";
    [ObservableProperty] private bool _deviceUsedFallback;

    private bool CanLoadDeviceInfo() => !IsDeviceInfoLoading;

    [RelayCommand(CanExecute = nameof(CanLoadDeviceInfo))]
    private async Task LoadDeviceInfo()
    {
        _deviceInfoCts?.Cancel();
        _deviceInfoCts?.Dispose();
        _deviceInfoCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = _deviceInfoCts.Token;

        IsDeviceInfoLoading = true;
        LoadDeviceInfoCommand.NotifyCanExecuteChanged();

        ResetDeviceInfoDisplay();

        try
        {
            var infoTask = _fiscalDevice.GetDetailedInfoAsync();

            var completed = await Task.WhenAny(infoTask, Task.Delay(Timeout.Infinite, ct));
            if (completed != infoTask)
                throw new OperationCanceledException(ct);

            var info = await infoTask;

            DeviceInfoSuccess = info.Success;
            HasDeviceInfo = true;

            DeviceRespondingBadge = info.RespondingDeviceBadge;
            DeviceUsedFallback = info.UsedFallback;

            if (!info.Success)
            {
                DeviceInfoError = info.ErrorMessage ?? "Erreur inconnue";
                await AppEventBus.PublishAsync(new AppEventArgs
                { Event = AppEvent.FiscalDeviceStatusChanged });
                return;
            }

            DeviceInfoType = info.DeviceTypeLabel;
            DeviceNIM = info.NIM ?? "—";
            DeviceNIF = info.NIF ?? "—";

            DeviceConnectionStatus = info.ConnectionStatus ?? "DIS";
            DeviceConnectionLabel = info.ConnectionStatus switch
            {
                "CON" => "✓ Connecté au serveur DGI",
                "TRA" => "⟳ Transmission en cours...",
                "RES" => "⟳ Restauration en cours...",
                _ => "✗ Déconnecté du serveur DGI"
            };

            DeviceLastSync = info.LastServerConnection?.ToString("dd/MM/yyyy HH:mm:ss", Inv) ?? "—";
            DeviceDateTime = info.DeviceDateTime?.ToString("dd/MM/yyyy HH:mm:ss", Inv) ?? "—";

            DeviceTaxpayerName = info.TaxpayerName ?? "—";
            DeviceTaxpayerAddress = info.TaxpayerAddress ?? "—";
            DeviceTaxpayerCity = info.TaxpayerCity ?? "—";
            DeviceTaxpayerPhone = info.TaxpayerPhone ?? "—";
            DeviceTaxpayerEmail = info.TaxpayerEmail ?? "—";

            DeviceTotalTransactions = info.TotalTransactions.ToString("N0", Inv);
            DeviceSalesCount = info.SalesInvoiceCount.ToString("N0", Inv);
            DeviceCreditNoteCount = info.CreditNoteCount.ToString("N0", Inv);
            DeviceTransactionsSent = info.TransactionsSent.ToString("N0", Inv);
            DeviceTransactionsInDevice = info.TransactionsInDevice.ToString("N0", Inv);
            DevicePendingCount = info.PendingRequestsCount.ToString(Inv);

            DeviceLastInvoice = info.LastInvoiceDate.HasValue
                ? $"{info.LastInvoiceType ?? ""} {info.LastInvoiceNumber ?? ""}" +
                  $" — {info.LastInvoiceDate.Value.ToString("dd/MM/yyyy HH:mm", Inv)}" +
                  (info.LastInvoiceAmount.HasValue
                      ? $" — {info.LastInvoiceAmount.Value.ToString("N0", Inv)} CDF" : "")
                : "Aucune facture";

            DeviceTokenValid = info.TokenValidUntil?.ToString("dd/MM/yyyy HH:mm", Inv) ?? "—";
            DeviceApiVersion = info.ApiVersion ?? "—";
            DeviceEmcfStatus = info.EmcfStatus ?? "—";
            DeviceLastError = info.LastError ?? "Aucune";

            var rates = info.TaxRates ?? Array.Empty<decimal>();
            var taxGroups = new[] { "A","B","C","D","E","F","G","H",
                                "I","J","K","L","M","N","O","P" };
            var activeTaxes = new List<string>();
            for (int i = 0; i < Math.Min(taxGroups.Length, rates.Length); i++)
            {
                if (rates[i] != 0)
                    activeTaxes.Add($"{taxGroups[i]}={rates[i].ToString("F1", Inv)}%");
            }
            DeviceTaxRatesDisplay = activeTaxes.Count > 0
                ? string.Join("  |  ", activeTaxes)
                : "Aucun taux configuré";

            DeviceCurrencyRates.Clear();
            if (info.CurrencyRates != null)
            {
                foreach (var cr in info.CurrencyRates)
                {
                    DeviceCurrencyRates.Add(new CurrencyRateDisplayItem
                    {
                        Code = cr.Code ?? "?",
                        Description = cr.Description ?? "",
                        Date = cr.Date != default ? cr.Date.ToString("dd/MM/yyyy", Inv) : "—",
                        Rate = cr.Rate.ToString("N2", Inv)
                    });
                }

                var usd = info.CurrencyRates.FirstOrDefault(r =>
                    string.Equals(r?.Code, "USD", StringComparison.OrdinalIgnoreCase));
                if (usd != null && usd.Rate > 0)
                    ApplyDgiUsdRateToUi(usd.Rate, usd.Date);
            }

            DeviceEmcfList.Clear();
            if (info.EmcfDevices != null)
            {
                foreach (var dev in info.EmcfDevices)
                {
                    DeviceEmcfList.Add(new EmcfDeviceDisplayItem
                    {
                        NIM = dev.NIM ?? "?",
                        Status = dev.Status ?? "?",
                        ShopName = dev.ShopName ?? "—",
                        Address = dev.Address ?? "—",
                        IsActive = string.Equals(dev.NIM, info.NIM, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            // ─────────────────────────────────────────────────────────
            // 🆕 HEALTH REPORT — synthetic verdict from C2h fields.
            //
            // We only attempt this for MCF (serial). For e-MCF the API
            // already returns a clean status, so we just mirror the C2h
            // fields we already have and skip the synthetic report.
            // ─────────────────────────────────────────────────────────
            await TryLoadHealthReportAsync(info);

            await AppEventBus.PublishAsync(new AppEventArgs
            { Event = AppEvent.FiscalDeviceStatusChanged });
        }
        catch (OperationCanceledException)
        {
            DeviceInfoSuccess = false;
            DeviceInfoError = "Délai dépassé (20 s). Le dispositif n'a pas répondu. Vérifiez la connexion réseau ou le port série.";
            HasDeviceInfo = true;
            await AppEventBus.PublishAsync(new AppEventArgs
            { Event = AppEvent.FiscalDeviceStatusChanged });
        }
        catch (Exception ex)
        {
            DeviceInfoSuccess = false;
            DeviceInfoError = ex.Message;
            HasDeviceInfo = true;
            await AppEventBus.PublishAsync(new AppEventArgs
            { Event = AppEvent.FiscalDeviceStatusChanged });
        }
        finally
        {
            IsDeviceInfoLoading = false;
            LoadDeviceInfoCommand.NotifyCanExecuteChanged();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // HEALTH REPORT — populates the synthetic verdict (MCF only).
    //
    // This works whether _fiscalDevice is a direct McfSerialClient or
    // wrapped by FiscalDeviceResolver. For e-MCF or unknown wrappers,
    // we synthesize a basic verdict from the C1h/C2h data we already
    // have, so the UI always shows something meaningful.
    // ══════════════════════════════════════════════════════════════
    private async Task TryLoadHealthReportAsync(FiscalDeviceDetailedInfo info)
    {
        try
        {
            // 1. Try the real health report (MCF only).
            var mcf = ResolveMcfClient(_fiscalDevice);
            if (mcf != null)
            {
                var report = await mcf.GetHealthReportAsync();
                ApplyHealthReport(report);
                return;
            }

            // 2. Fallback: synthesize a verdict from FiscalDeviceDetailedInfo
            //    (covers e-MCF and any future device type).
            ApplySyntheticHealthFromDetailedInfo(info);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Settings] Health report failed: {ex.Message}");
            DeviceHealthStatus = "Unknown";
            DeviceHealthLabel = "État inconnu";
            DeviceHealthSummary = $"Impossible de calculer l'état : {ex.Message}";
            DeviceHealthBadgeKind = "neutral";
            DeviceHealthWarnings.Clear();
            HasHealthReport = true;
        }
    }

    private static McfSerialClient? ResolveMcfClient(IFiscalDeviceService svc)
    {
        if (svc is McfSerialClient direct) return direct;

        if (svc is FiscalDeviceResolver r)
        {
            // Prefer the active device, then primary, then fallback.
            if (r.CurrentDevice is McfSerialClient curr) return curr;
            if (r.PrimaryDevice is McfSerialClient pri) return pri;
            if (r.FallbackDevice is McfSerialClient fb) return fb;
        }

        return null;
    }

    private void ApplyHealthReport(McfHealthReport report)
    {
        DeviceHealthWarnings.Clear();
        foreach (var w in report.Warnings)
            DeviceHealthWarnings.Add(w);

        DeviceHealthStatus = report.Status.ToString();
        DeviceHealthSummary = report.Summary;

        (DeviceHealthLabel, DeviceHealthBadgeKind) = report.Status switch
        {
            McfHealth.Healthy => ("✓ Opérationnel", "ok"),
            McfHealth.Degraded => ("⚠ Dégradé", "warn"),
            McfHealth.Unhealthy => ("✗ Critique", "error"),
            _ => ("? État inconnu", "neutral")
        };

        DeviceHealthSyncAge = report.TimeSinceLastSync.HasValue
            ? $"il y a {FormatAge(report.TimeSinceLastSync.Value)}"
            : "jamais synchronisé";

        HasHealthReport = true;
    }

    private void ApplySyntheticHealthFromDetailedInfo(FiscalDeviceDetailedInfo info)
    {
        // Lightweight verdict for non-MCF devices (e-MCF, etc.).
        DeviceHealthWarnings.Clear();

        var pending = info.PendingRequestsCount;
        var lastSync = info.LastServerConnection;
        var hasError = !string.IsNullOrWhiteSpace(info.LastError)
                       && !string.Equals(info.LastError, "Aucune", StringComparison.OrdinalIgnoreCase);

        TimeSpan? age = lastSync.HasValue
            ? _time.LocalNow - lastSync.Value
            : null;

        var status = "Healthy";
        if (pending > 50 || (age?.TotalHours ?? 0) > 6 || hasError)
        {
            status = "Unhealthy";
            if (pending > 50) DeviceHealthWarnings.Add($"File d'attente saturée : {pending} transactions.");
            if ((age?.TotalHours ?? 0) > 6) DeviceHealthWarnings.Add($"Synchronisation trop ancienne : {FormatAge(age!.Value)}.");
            if (hasError) DeviceHealthWarnings.Add($"Dernière erreur : {info.LastError}");
        }
        else if (pending > 5 || (age?.TotalHours ?? 0) > 1)
        {
            status = "Degraded";
            if (pending > 5) DeviceHealthWarnings.Add($"File d'attente en croissance : {pending} transactions.");
            if ((age?.TotalHours ?? 0) > 1) DeviceHealthWarnings.Add($"Dernière sync : {FormatAge(age!.Value)}.");
        }

        DeviceHealthStatus = status;
        (DeviceHealthLabel, DeviceHealthBadgeKind) = status switch
        {
            "Healthy" => ("✓ Opérationnel", "ok"),
            "Degraded" => ("⚠ Dégradé", "warn"),
            "Unhealthy" => ("✗ Critique", "error"),
            _ => ("? Inconnu", "neutral")
        };

        DeviceHealthSummary = status switch
        {
            "Healthy" => $"OK — {info.TransactionsSent:N0} transactions envoyées, {pending} en attente.",
            "Degraded" => $"Dégradé — {DeviceHealthWarnings.Count} avertissement(s).",
            "Unhealthy" => $"Critique — {DeviceHealthWarnings.Count} problème(s) détecté(s).",
            _ => "État inconnu."
        };

        DeviceHealthSyncAge = age.HasValue
            ? $"il y a {FormatAge(age.Value)}"
            : "jamais synchronisé";

        HasHealthReport = true;
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1) return $"{age.TotalDays:F1} j";
        if (age.TotalHours >= 1) return $"{age.TotalHours:F1} h";
        if (age.TotalMinutes >= 1) return $"{age.TotalMinutes:F0} min";
        return $"{age.TotalSeconds:F0} s";
    }

    private void ResetDeviceInfoDisplay()
    {
        HasDeviceInfo = false;
        DeviceInfoSuccess = false;
        DeviceInfoError = "";

        DeviceInfoType = "—";
        DeviceNIM = "—";
        DeviceNIF = "—";
        DeviceConnectionStatus = "DIS";
        DeviceConnectionLabel = "Déconnecté";
        DeviceLastSync = "—";
        DeviceDateTime = "—";
        DeviceTaxpayerName = "—";
        DeviceTaxpayerAddress = "—";
        DeviceTaxpayerCity = "—";
        DeviceTaxpayerPhone = "—";
        DeviceTaxpayerEmail = "—";
        DeviceTotalTransactions = "0";
        DeviceSalesCount = "0";
        DeviceCreditNoteCount = "0";
        DeviceTransactionsSent = "0";
        DeviceTransactionsInDevice = "0";
        DevicePendingCount = "0";
        DeviceLastInvoice = "—";
        DeviceTokenValid = "—";
        DeviceApiVersion = "—";
        DeviceEmcfStatus = "—";
        DeviceLastError = "—";
        DeviceTaxRatesDisplay = "";
        DeviceRespondingBadge = "—";
        DeviceUsedFallback = false;

        DeviceCurrencyRates.Clear();
        DeviceEmcfList.Clear();

        // 🆕 Health report reset
        HasHealthReport = false;
        DeviceHealthStatus = "Unknown";
        DeviceHealthLabel = "—";
        DeviceHealthSummary = "";
        DeviceHealthBadgeKind = "neutral";
        DeviceHealthSyncAge = "—";
        DeviceHealthWarnings.Clear();
    }

    partial void OnIsDeviceInfoLoadingChanged(bool value)
        => LoadDeviceInfoCommand.NotifyCanExecuteChanged();

    // ══════════════════════════════════════════════════════════════
    // TEST CONNEXION
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task TestConnection()
    {
        IsBusy = true;
        IsTestingConnection = true;
        HasTestResult = false;
        ShowSaveSuccess = false;
        ShowSaveError = false;

        var sw = Stopwatch.StartNew();
        var nowLocal = _time.LocalNow;

        // 🆕 Take exclusive ownership of the port for the whole test.
        IDisposable? lease = null;
        if (_fiscalDevice is FiscalDeviceResolver resolver)
        {
            try { lease = await resolver.AcquireExclusiveAccessAsync(); }
            catch (Exception leaseEx)
            {
                Debug.WriteLine($"[Settings] Lease acquisition failed: {leaseEx.Message}");
            }
        }

        try
        {
            if (IsEmcfSelected)
            {
                SaveStatus = "Test connexion e-MCF...";

                if (string.IsNullOrWhiteSpace(EmcfApiUrl) || string.IsNullOrWhiteSpace(EmcfToken))
                {
                    SaveStatus = "URL et Token obligatoires";
                    ShowSaveError = true;
                    TestSuccess = false;
                    TestStatus = "VALIDATION";
                    TestMessage = "L'URL de l'API et le Token JWT sont obligatoires.";
                    TestResponseTime = "—";
                    TestNIM = "—";
                    TestServerVersion = "—";
                    TestDetails = $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}";
                    TestRawResponse = "(aucun appel effectué)";
                    HasTestResult = true;
                    return;
                }

                using var client = new EMcfHttpClient(EmcfApiUrl, EmcfToken, CompanyNIF, _time);
                var status = await client.GetStatusAsync();
                sw.Stop();

                TestResponseTime = $"{sw.ElapsedMilliseconds} ms";

                if (status.Success)
                {
                    EmcfNIM = status.NIM ?? EmcfNIM;
                    SaveStatus = $"✓ e-MCF connecté — NIM: {status.NIM}";
                    ShowSaveSuccess = true;
                    TestSuccess = true;
                    TestStatus = "CONNECTÉ";
                    TestMessage = "Connexion e-MCF réussie. Machine identifiée avec succès.";
                    TestNIM = status.NIM ?? "—";
                    TestServerVersion = "e-MCF (API REST)";
                    TestDetails =
                        $"Mode : e-MCF (API REST)\n" +
                        $"URL : {EmcfApiUrl}\n" +
                        $"NIF : {CompanyNIF}\n" +
                        $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}\n" +
                        $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"{{ \"success\": true, \"nim\": \"{status.NIM}\" }}";

                    await AppEventBus.PublishAsync(new AppEventArgs
                    { Event = AppEvent.FiscalDeviceStatusChanged });
                }
                else
                {
                    SaveStatus = $"✗ Échec: {status.ErrorMessage}";
                    ShowSaveError = true;
                    TestSuccess = false;
                    TestStatus = "ÉCHEC";
                    TestMessage = status.ErrorMessage ?? "Erreur de connexion inconnue.";
                    TestNIM = "—";
                    TestServerVersion = "—";
                    TestDetails =
                        $"Mode : e-MCF (API REST)\n" +
                        $"URL : {EmcfApiUrl}\n" +
                        $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}\n" +
                        $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"{{ \"success\": false, \"error\": \"{status.ErrorMessage}\" }}";

                    await AppEventBus.PublishAsync(new AppEventArgs
                    { Event = AppEvent.FiscalDeviceStatusChanged });
                }

                HasTestResult = true;
            }
            else
            {
                if (SelectedComPort == "(aucun port détecté)" || string.IsNullOrWhiteSpace(SelectedComPort))
                {
                    SaveStatus = "Aucun port série disponible.";
                    ShowSaveError = true;
                    TestSuccess = false;
                    TestStatus = "VALIDATION";
                    TestMessage = "Aucun port série détecté. Branchez le MCF et cliquez « Rafraîchir ».";
                    TestResponseTime = "—";
                    TestNIM = "—";
                    TestServerVersion = "—";
                    TestDetails = $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}";
                    TestRawResponse = "(aucun appel effectué)";
                    HasTestResult = true;
                    return;
                }

                SaveStatus = $"Test connexion MCF sur {SelectedComPort}...";

                // 🆕 Explicit dispose so we can null-check on Connect-throw.
                McfSerialClient? mcfClient = null;
                try
                {
                    mcfClient = new McfSerialClient(SelectedComPort, _time, BaudRate);
                    mcfClient.Connect();
                    var status = await mcfClient.GetStatusAsync();
                    sw.Stop();

                    TestResponseTime = $"{sw.ElapsedMilliseconds} ms";

                    if (status.Success)
                    {
                        SaveStatus = $"✓ MCF détecté — NIM: {status.NIM}, NIF: {status.NIF}";
                        ShowSaveSuccess = true;
                        TestSuccess = true;
                        TestStatus = "CONNECTÉ";
                        TestMessage = $"MCF détecté et opérationnel sur {SelectedComPort}.";
                        TestNIM = status.NIM ?? "—";
                        TestServerVersion = "MCF (Port Série)";
                        TestDetails =
                            $"Mode : MCF (Port Série)\n" +
                            $"Port : {SelectedComPort}\n" +
                            $"Baud Rate : {BaudRate}\n" +
                            $"NIF : {status.NIF}\n" +
                            $"NIM : {status.NIM}\n" +
                            $"Format : 8N1\n" +
                            $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}\n" +
                            $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                        TestRawResponse = $"NIM={status.NIM}, NIF={status.NIF}, Success=true";

                        await AppEventBus.PublishAsync(new AppEventArgs
                        { Event = AppEvent.FiscalDeviceStatusChanged });
                    }
                    else
                    {
                        SaveStatus = $"✗ Échec: {status.ErrorMessage}";
                        ShowSaveError = true;
                        TestSuccess = false;
                        TestStatus = "ÉCHEC";
                        TestMessage = status.ErrorMessage ?? "Impossible de communiquer avec le MCF.";
                        TestNIM = "—";
                        TestServerVersion = "—";
                        TestDetails =
                            $"Mode : MCF (Port Série)\n" +
                            $"Port : {SelectedComPort}\n" +
                            $"Baud Rate : {BaudRate}\n" +
                            $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}\n" +
                            $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                        TestRawResponse = $"Error: {status.ErrorMessage}";

                        await AppEventBus.PublishAsync(new AppEventArgs
                        { Event = AppEvent.FiscalDeviceStatusChanged });
                    }

                    HasTestResult = true;
                }
                finally
                {
                    mcfClient?.Dispose();
                }
            }

            await Task.Delay(4000);
            ShowSaveSuccess = false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            SaveStatus = $"Échec connexion : {ex.Message}";
            ShowSaveError = true;
            TestSuccess = false;
            TestStatus = "EXCEPTION";
            TestMessage = ex.Message;
            TestResponseTime = $"{sw.ElapsedMilliseconds} ms";
            TestNIM = "—";
            TestServerVersion = "—";
            TestDetails =
                $"Exception : {ex.GetType().Name}\n" +
                $"Testé le : {nowLocal:dd/MM/yyyy HH:mm:ss}\n" +
                $"Temps : {sw.ElapsedMilliseconds} ms";
            TestRawResponse = ex.ToString();
            HasTestResult = true;

            await AppEventBus.PublishAsync(new AppEventArgs
            { Event = AppEvent.FiscalDeviceStatusChanged });
        }
        finally
        {
            IsBusy = false;
            IsTestingConnection = false;
            // 🆕 Release the lease so the resolver can rebuild on next request.
            lease?.Dispose();
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LICENCE
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private Task ActivateLicense()
    {
        LicenseMessage = "";

        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            LicenseMessage = "Veuillez entrer une clé de licence.";
            return Task.CompletedTask;
        }

        var parts = LicenseKey.Trim().Split('-');
        if (parts.Length != 4 || parts[0] != "GECOM")
        {
            LicenseMessage = "Format invalide. Attendu : GECOM-XXXXX-XXXXX-XXXXX";
            return Task.CompletedTask;
        }

        LicensePlan = "Pro";
        LicenseStatus = "Activée ✓";
        LicenseMessage = "";
        SaveStatus = "Licence activée avec succès !";
        ShowSaveSuccess = true;

        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════

    private void ApplyDgiUsdRateToUi(decimal rate, DateTimeOffset dgiDate)
    {
        var effectiveDate = dgiDate != default
            ? dgiDate
            : _time.UtcNow;

        CurrentExchangeRate = rate.ToString("F2", Inv);
        DgiUsdRate = rate.ToString("N2", Inv);
        DgiUsdDate = effectiveDate.ToString("dd/MM/yyyy", Inv);
        _dgiExchangeRateDate = effectiveDate;
        HasDgiRate = true;
    }

    private async Task PersistDgiUsdRateAsync(decimal rate, DateTimeOffset? dgiDate)
    {
        try
        {
            var current = await _settingsService.LoadSettingsAsync();
            current.CurrentExchangeRate = rate;
            current.DgiExchangeRateDate = dgiDate;
            await _settingsService.SaveSettingsAsync(current);

            BroadcastExchangeRates();

            Debug.WriteLine($"[Settings] DGI rate saved: 1 USD = {rate.ToString("N2", Inv)} CDF");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Settings] Auto-save DGI rate failed: {ex.Message}");
        }
    }

    private static bool TryParseDecimalStrict(
        string? input,
        out decimal value,
        out string error,
        bool allowZero = false)
    {
        value = 0m;
        error = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "valeur vide";
            return false;
        }

        if (!decimal.TryParse(input.Trim(), NumberStyles.Any, Inv, out value))
        {
            error = $"« {input} » n'est pas un nombre valide";
            return false;
        }

        if (!allowZero && value <= 0m)
        {
            error = "la valeur doit être strictement positive";
            return false;
        }

        if (allowZero && value < 0m)
        {
            error = "la valeur ne peut pas être négative";
            return false;
        }

        return true;
    }
}

// ══════════════════════════════════════════════════════════════
// DISPLAY ITEM CLASSES
// ══════════════════════════════════════════════════════════════

public class CurrencyRateDisplayItem
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Date { get; set; } = "";
    public string Rate { get; set; } = "";
}

public class EmcfDeviceDisplayItem
{
    public string NIM { get; set; } = "";
    public string Status { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string Address { get; set; } = "";
    public bool IsActive { get; set; }
}