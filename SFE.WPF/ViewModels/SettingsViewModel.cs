using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using SFE.WPF.Messages;
using SFE.WPF.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SFE.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly IFiscalDeviceService _fiscalDevice;
    private int _companyId;
    private int _activePosId;
    private bool _isLoading;

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
    private DateTime? _dgiExchangeRateDate;

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
        IFiscalDeviceService fiscalDevice)
    {
        _settingsService = settingsService;
        _authService = authService;
        _fiscalDevice = fiscalDevice;
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

    /// <summary>
    /// Broadcasts the current exchange rates so any open POS/Invoice 
    /// screen picks them up immediately without restart.
    /// </summary>
    private void BroadcastExchangeRates()
    {
        decimal.TryParse(CurrentExchangeRate, out var usd);
        decimal.TryParse(CurrentExchangeRateEUR, out var eur);
        decimal.TryParse(CurrentExchangeRateCNY, out var cny);

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

            IsEmcfSelected = data.DeviceType == DeviceType.EMcf;
            EmcfApiUrl = data.EmcfApiUrl;
            EmcfToken = data.EmcfToken;
            EmcfNIM = data.EmcfNIM;

            _companyLogoBytes = data.CompanyLogo;
            CompanyLogoPreview = BytesToImage(_companyLogoBytes);
            HasLogo = _companyLogoBytes is { Length: > 0 };

            // ── COM Port ──
            if (!string.IsNullOrEmpty(data.McfPortName)
                && AvailableComPorts.Contains(data.McfPortName))
            {
                SelectedComPort = data.McfPortName;
            }
            else if (AvailableComPorts.Count > 0
                && AvailableComPorts[0] != "(aucun port détecté)")
            {
                SelectedComPort = AvailableComPorts.First();
            }
            else
            {
                SelectedComPort = AvailableComPorts.FirstOrDefault() ?? "";
            }

            BaudRate = data.McfBaudRate > 0 ? data.McfBaudRate : 115200;

            IsPriceModeTTC = data.DefaultPriceMode == PriceMode.TTC;
            DiscountBeforeTax = data.DiscountBeforeTax;
            DefaultCurrency = data.DefaultCurrency;
            CurrentExchangeRate = data.CurrentExchangeRate.ToString("F2");
            CurrentExchangeRateEUR = data.CurrentExchangeRateEUR.ToString("F2");
            CurrentExchangeRateCNY = data.CurrentExchangeRateCNY.ToString("F2");
            ExchangeRateMode = data.ExchangeRateMode;

            // ── Restore DGI rate info ──
            _dgiExchangeRateDate = data.DgiExchangeRateDate;
            if (_dgiExchangeRateDate.HasValue && data.CurrentExchangeRate > 0)
            {
                DgiUsdRate = data.CurrentExchangeRate.ToString("N2");
                DgiUsdDate = _dgiExchangeRateDate.Value.ToString("dd/MM/yyyy");
                HasDgiRate = true;
            }

            IsLoyaltyEnabled = data.LoyaltyEnabled;
            LoyaltyEarnRate = data.LoyaltyEarnRate.ToString("0");
            LoyaltyRedeemRate = data.LoyaltyRedeemRate.ToString("0");
            LoyaltyMinRedeemPoints = data.LoyaltyMinRedeemPoints.ToString();

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

    // ══════════════════════════════════════════════════════════════
    // SAVE SETTINGS — persists everything to database
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
            decimal.TryParse(CurrentExchangeRate, out var rate);

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
                CurrentExchangeRate = rate > 0 ? rate : 2800m,
                CurrentExchangeRateEUR = decimal.TryParse(CurrentExchangeRateEUR, out var rateEUR) ? rateEUR : 3100m,
                CurrentExchangeRateCNY = decimal.TryParse(CurrentExchangeRateCNY, out var rateCNY) ? rateCNY : 385m,
                ExchangeRateMode = ExchangeRateMode,
                DgiExchangeRateDate = _dgiExchangeRateDate,   // ★ persist DGI date
                LoyaltyEnabled = IsLoyaltyEnabled,
                LoyaltyEarnRate = decimal.TryParse(LoyaltyEarnRate, out var earn) ? earn : 1000m,
                LoyaltyRedeemRate = decimal.TryParse(LoyaltyRedeemRate, out var redeem) ? redeem : 500m,
                LoyaltyMinRedeemPoints = int.TryParse(LoyaltyMinRedeemPoints, out var minPts) ? minPts : 100,
                DeploymentMode = DeploymentMode.Standalone,
                ActivePosId = _activePosId,
                DeviceType = IsEmcfSelected ? DeviceType.EMcf : DeviceType.Mcf,
                EmcfApiUrl = EmcfApiUrl,
                EmcfToken = EmcfToken,
                EmcfNIM = EmcfNIM,
                McfPortName = SelectedComPort,
                McfBaudRate = BaudRate,
            };

            await _settingsService.SaveSettingsAsync(data);

            // Force fiscal device resolver to rebuild with new settings
            if (_fiscalDevice is FiscalDeviceResolver resolver)
                resolver.Invalidate();

            // ★ Broadcast all changed settings so the rest of the app picks them up
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
    // FETCH DGI CURRENCY RATES — retrieves USD rate, saves to DB
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
                // ★ Better message for MCF that doesn't return rates
                DgiRateStatus = IsEmcfSelected
                    ? "Aucun taux de change retourné par la DGI."
                    : "Le MCF n'a pas retourné de taux de change. Vérifiez que le dispositif est synchronisé avec le serveur DGI.";
                ShowDgiRateError = true;
                return;
            }

            // ── DGI returns USD rate ──
            var usdRate = info.CurrencyRates.FirstOrDefault(r =>
                r.Code.Equals("USD", StringComparison.OrdinalIgnoreCase));

            if (usdRate == null || usdRate.Rate <= 0)
            {
                DgiRateStatus = "Taux USD non trouvé dans la réponse DGI.";
                ShowDgiRateError = true;
                return;
            }

            // ── Update UI fields ──
            CurrentExchangeRate = usdRate.Rate.ToString("F2");
            DgiUsdRate = usdRate.Rate.ToString("N2");
            DgiUsdDate = usdRate.Date != default
                ? usdRate.Date.ToString("dd/MM/yyyy")
                : DateTime.Now.ToString("dd/MM/yyyy");
            _dgiExchangeRateDate = usdRate.Date != default ? usdRate.Date : DateTime.Now;
            HasDgiRate = true;

            // ── Update device info display table ──
            DeviceCurrencyRates.Clear();
            foreach (var cr in info.CurrencyRates)
            {
                DeviceCurrencyRates.Add(new CurrencyRateDisplayItem
                {
                    Code = cr.Code ?? "?",
                    Description = cr.Description ?? "",
                    Date = cr.Date != default ? cr.Date.ToString("dd/MM/yyyy") : "—",
                    Rate = cr.Rate.ToString("N2")
                });
            }

            // ── Auto-save to database so the value persists ──
            try
            {
                var currentSettings = await _settingsService.LoadSettingsAsync();
                currentSettings.CurrentExchangeRate = usdRate.Rate;
                currentSettings.DgiExchangeRateDate = _dgiExchangeRateDate;
                await _settingsService.SaveSettingsAsync(currentSettings);

                // ★ Broadcast so POS/Invoice screens pick it up immediately
                BroadcastExchangeRates();

                Debug.WriteLine($"[Settings] DGI rate saved: 1 USD = {usdRate.Rate:N2} CDF");
            }
            catch (Exception saveEx)
            {
                Debug.WriteLine($"[Settings] Failed to auto-save DGI rate: {saveEx.Message}");
                // Non-fatal: the UI already shows the rate, user can click Save manually
            }

            DgiRateStatus = $"✓ Taux USD appliqué : 1 USD = {usdRate.Rate:N2} CDF — Date DGI : {DgiUsdDate}";
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

    [RelayCommand]
    private async Task LoadDeviceInfo()
    {
        IsDeviceInfoLoading = true;
        HasDeviceInfo = false;
        DeviceInfoSuccess = false;
        DeviceInfoError = "";

        try
        {
            var info = await _fiscalDevice.GetDetailedInfoAsync();

            DeviceInfoSuccess = info.Success;
            HasDeviceInfo = true;

            if (!info.Success)
            {
                DeviceInfoError = info.ErrorMessage ?? "Erreur inconnue";
                await AppEventBus.PublishAsync(new AppEventArgs
                {
                    Event = AppEvent.FiscalDeviceStatusChanged
                });
                return;
            }

            // ── Identity ──
            DeviceInfoType = info.DeviceTypeLabel;
            DeviceNIM = info.NIM ?? "—";
            DeviceNIF = info.NIF ?? "—";

            // ── Connection ──
            DeviceConnectionStatus = info.ConnectionStatus ?? "DIS";
            DeviceConnectionLabel = info.ConnectionStatus switch
            {
                "CON" => "✓ Connecté au serveur DGI",
                "TRA" => "⟳ Transmission en cours...",
                "RES" => "⟳ Restauration en cours...",
                _ => "✗ Déconnecté du serveur DGI"
            };

            DeviceLastSync = info.LastServerConnection.HasValue
                ? info.LastServerConnection.Value.ToString("dd/MM/yyyy HH:mm:ss")
                : "—";

            DeviceDateTime = info.DeviceDateTime.HasValue
                ? info.DeviceDateTime.Value.ToString("dd/MM/yyyy HH:mm:ss")
                : "—";

            // ── Taxpayer ──
            DeviceTaxpayerName = info.TaxpayerName ?? "—";
            DeviceTaxpayerAddress = info.TaxpayerAddress ?? "—";
            DeviceTaxpayerCity = info.TaxpayerCity ?? "—";
            DeviceTaxpayerPhone = info.TaxpayerPhone ?? "—";
            DeviceTaxpayerEmail = info.TaxpayerEmail ?? "—";

            // ── Counters ──
            DeviceTotalTransactions = info.TotalTransactions.ToString("N0");
            DeviceSalesCount = info.SalesInvoiceCount.ToString("N0");
            DeviceCreditNoteCount = info.CreditNoteCount.ToString("N0");
            DeviceTransactionsSent = info.TransactionsSent.ToString("N0");
            DeviceTransactionsInDevice = info.TransactionsInDevice.ToString("N0");
            DevicePendingCount = info.PendingRequestsCount.ToString();

            // ── Last Invoice ──
            if (info.LastInvoiceDate.HasValue)
            {
                DeviceLastInvoice = $"{info.LastInvoiceType ?? ""} {info.LastInvoiceNumber ?? ""}" +
                    $" — {info.LastInvoiceDate.Value:dd/MM/yyyy HH:mm}" +
                    (info.LastInvoiceAmount.HasValue ? $" — {info.LastInvoiceAmount.Value:N0} CDF" : "");
            }
            else
            {
                DeviceLastInvoice = "Aucune facture";
            }

            // ── e-MCF specific ──
            DeviceTokenValid = info.TokenValidUntil?.ToString("dd/MM/yyyy HH:mm") ?? "—";
            DeviceApiVersion = info.ApiVersion ?? "—";
            DeviceEmcfStatus = info.EmcfStatus ?? "—";
            DeviceLastError = info.LastError ?? "Aucune";

            // ── Tax Rates ──
            var taxGroups = new[] { "A", "B", "C", "D", "E", "F", "G", "H",
                                    "I", "J", "K", "L", "M", "N", "O", "P" };
            var activeTaxes = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                if (info.TaxRates[i] != 0)
                    activeTaxes.Add($"{taxGroups[i]}={info.TaxRates[i]:F1}%");
            }
            DeviceTaxRatesDisplay = activeTaxes.Count > 0
                ? string.Join("  |  ", activeTaxes)
                : "Aucun taux configuré";

            // ── Currency Rates (display + auto-apply USD) ──
            DeviceCurrencyRates.Clear();
            if (info.CurrencyRates != null)
            {
                foreach (var cr in info.CurrencyRates)
                {
                    DeviceCurrencyRates.Add(new CurrencyRateDisplayItem
                    {
                        Code = cr.Code ?? "?",
                        Description = cr.Description ?? "",
                        Date = cr.Date != default ? cr.Date.ToString("dd/MM/yyyy") : "—",
                        Rate = cr.Rate.ToString("N2")
                    });
                }

                // ★ Auto-apply USD rate from DGI
                var usd = info.CurrencyRates.FirstOrDefault(r =>
                    r.Code.Equals("USD", StringComparison.OrdinalIgnoreCase));
                if (usd != null && usd.Rate > 0)
                {
                    DgiUsdRate = usd.Rate.ToString("N2");
                    DgiUsdDate = usd.Date != default ? usd.Date.ToString("dd/MM/yyyy") : "—";
                    _dgiExchangeRateDate = usd.Date != default ? usd.Date : DateTime.Now;
                    HasDgiRate = true;

                    // Update the settings field
                    CurrentExchangeRate = usd.Rate.ToString("F2");

                    // ★ Auto-save to database
                    try
                    {
                        var currentSettings = await _settingsService.LoadSettingsAsync();
                        currentSettings.CurrentExchangeRate = usd.Rate;
                        currentSettings.DgiExchangeRateDate = _dgiExchangeRateDate;
                        await _settingsService.SaveSettingsAsync(currentSettings);
                        BroadcastExchangeRates();
                    }
                    catch (Exception saveEx)
                    {
                        Debug.WriteLine($"[Settings] Auto-save DGI rate failed: {saveEx.Message}");
                    }
                }
            }

            // ── e-MCF Devices List ──
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
                        IsActive = dev.NIM == info.NIM
                    });
                }
            }

            // ★ Publish success
            await AppEventBus.PublishAsync(new AppEventArgs
            {
                Event = AppEvent.FiscalDeviceStatusChanged
            });
        }
        catch (Exception ex)
        {
            DeviceInfoSuccess = false;
            DeviceInfoError = ex.Message;
            HasDeviceInfo = true;

            await AppEventBus.PublishAsync(new AppEventArgs
            {
                Event = AppEvent.FiscalDeviceStatusChanged
            });
        }
        finally
        {
            IsDeviceInfoLoading = false;
        }
    }

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
                    TestDetails = $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    TestRawResponse = "(aucun appel effectué)";
                    HasTestResult = true;
                    return;
                }

                var client = new EMcfHttpClient(EmcfApiUrl, EmcfToken, CompanyNIF);
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
                    TestDetails = $"Mode : e-MCF (API REST)\nURL : {EmcfApiUrl}\nNIF : {CompanyNIF}\nTesté le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nTemps de réponse : {sw.ElapsedMilliseconds} ms";
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
                    TestDetails = $"Mode : e-MCF (API REST)\nURL : {EmcfApiUrl}\nTesté le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nTemps de réponse : {sw.ElapsedMilliseconds} ms";
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
                    TestDetails = $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    TestRawResponse = "(aucun appel effectué)";
                    HasTestResult = true;
                    return;
                }

                SaveStatus = $"Test connexion MCF sur {SelectedComPort}...";

                if (_fiscalDevice is FiscalDeviceResolver resolver)
                    resolver.Invalidate();

                using var mcfClient = new McfSerialClient(SelectedComPort, BaudRate);
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
                    TestDetails = $"Mode : MCF (Port Série)\nPort : {SelectedComPort}\nBaud Rate : {BaudRate}\nNIF : {status.NIF}\nNIM : {status.NIM}\nFormat : 8N1\nTesté le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nTemps de réponse : {sw.ElapsedMilliseconds} ms";
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
                    TestDetails = $"Mode : MCF (Port Série)\nPort : {SelectedComPort}\nBaud Rate : {BaudRate}\nTesté le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nTemps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"Error: {status.ErrorMessage}";

                    await AppEventBus.PublishAsync(new AppEventArgs
                    { Event = AppEvent.FiscalDeviceStatusChanged });
                }

                HasTestResult = true;
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
            TestDetails = $"Exception : {ex.GetType().Name}\nTesté le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nTemps : {sw.ElapsedMilliseconds} ms";
            TestRawResponse = ex.ToString();
            HasTestResult = true;

            await AppEventBus.PublishAsync(new AppEventArgs
            { Event = AppEvent.FiscalDeviceStatusChanged });
        }
        finally
        {
            IsBusy = false;
            IsTestingConnection = false;
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