using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SFE.Infrastructure.EMcf;
using SFE.Infrastructure.Mcf;
using SFE.WPF.Messages;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SFE.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
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

    // 🆕 ══════════ LOGO ══════════
    [ObservableProperty] private ImageSource? _companyLogoPreview;
    [ObservableProperty] private bool _hasLogo;
    private byte[]? _companyLogoBytes;

    // ══════════ DISPOSITIF FISCAL ══════════
    [ObservableProperty] private bool _isEmcfSelected = true;
    [ObservableProperty] private string _emcfApiUrl = "";
    [ObservableProperty] private string _emcfToken = "";
    [ObservableProperty] private string _emcfNIM = "";
    [ObservableProperty] private string _selectedComPort = "COM3";
    [ObservableProperty] private int _baudRate = 115200;

    // ══════════ COM PORTS ══════════
    public ObservableCollection<string> AvailableComPorts { get; } = new();
    public int[] AvailableBaudRates { get; } = { 9600, 19200, 38400, 57600, 115200 };

    // ══════════ MODE DE PRIX ══════════
    [ObservableProperty] private bool _isPriceModeTTC = true;

    // 🆕 ══════════ REMISE ══════════
    [ObservableProperty] private bool _discountBeforeTax = true;

    // 🆕 ══════════ DEVISE ══════════
    [ObservableProperty] private Currency _defaultCurrency = Currency.CDF;
    [ObservableProperty] private string _currentExchangeRate = "2800";
    [ObservableProperty] private ExchangeRateMode _exchangeRateMode = ExchangeRateMode.Manual;

    public Currency[] Currencies { get; } = Enum.GetValues<Currency>();
    public ExchangeRateMode[] ExchangeRateModes { get; } = Enum.GetValues<ExchangeRateMode>();

    // ══════════ FIDÉLITÉ ══════════
    [ObservableProperty] private bool _isLoyaltyEnabled;
    [ObservableProperty] private string _loyaltyEarnRate = "1000";
    [ObservableProperty] private string _loyaltyRedeemRate = "500";
    [ObservableProperty] private string _loyaltyMinRedeemPoints = "100";

    // ══════════ POS ══════════
    [ObservableProperty] private int _activePosCount;
    [ObservableProperty] private int _totalPosCount;

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

    // ══════════ LICENCE ══════════
    [ObservableProperty] private string _licenseKey = "";
    [ObservableProperty] private string _licenseStatus = "Non activée";
    [ObservableProperty] private string _licensePlan = "Free";
    [ObservableProperty] private string _licenseMessage = "";

    public PointOfSaleManagementViewModel PosManagement { get; }

    // ══════════════════════════════════════════════════════════════
    // CONSTRUCTEUR
    // ══════════════════════════════════════════════════════════════

    public SettingsViewModel(SettingsService settingsService, PointOfSaleManagementViewModel posManagement)
    {
        _settingsService = settingsService;
        PosManagement = posManagement;
        PageTitle = "Paramètres";

        RefreshComPorts();
        _ = LoadSettingsAsync();
        
    }

    // ══════════════════════════════════════════════════════════════
    // 🆕 LOGO COMMANDS
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

    /// <summary>🆕 Broadcast immédiat quand l'utilisateur change le toggle remise.</summary>
    partial void OnDiscountBeforeTaxChanged(bool value)
    {
        if (_isLoading) return;
        WeakReferenceMessenger.Default.Send(new DiscountBeforeTaxChangedMessage(value));
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
            await PosManagement.LoadCommand.ExecuteAsync(null);
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

            if (!string.IsNullOrEmpty(data.McfPortName) && AvailableComPorts.Contains(data.McfPortName))
                SelectedComPort = data.McfPortName;
            else if (AvailableComPorts.Count > 0)
                SelectedComPort = AvailableComPorts.First();

            BaudRate = data.McfBaudRate > 0 ? data.McfBaudRate : 115200;

            IsPriceModeTTC = data.DefaultPriceMode == PriceMode.TTC;

            // 🆕 Paramètres de calcul
            DiscountBeforeTax = data.DiscountBeforeTax;
            DefaultCurrency = data.DefaultCurrency;
            CurrentExchangeRate = data.CurrentExchangeRate.ToString("F0");
            ExchangeRateMode = data.ExchangeRateMode;

            IsLoyaltyEnabled = data.LoyaltyEnabled;
            LoyaltyEarnRate = data.LoyaltyEarnRate.ToString("0");
            LoyaltyRedeemRate = data.LoyaltyRedeemRate.ToString("0");
            LoyaltyMinRedeemPoints = data.LoyaltyMinRedeemPoints.ToString();

            ActivePosCount = data.ActivePosCount;
            TotalPosCount = data.TotalPosCount;

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
                CompanyLogo = _companyLogoBytes,      // 🆕
                DefaultPriceMode = IsPriceModeTTC ? PriceMode.TTC : PriceMode.HT,

                // 🆕 Paramètres de calcul
                DiscountBeforeTax = DiscountBeforeTax,
                DefaultCurrency = DefaultCurrency,
                CurrentExchangeRate = rate > 0 ? rate : 2800m,
                ExchangeRateMode = ExchangeRateMode,

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
                McfBaudRate = BaudRate
            };

            await _settingsService.SaveSettingsAsync(data);

            // Broadcast des changements
            WeakReferenceMessenger.Default.Send(
                new PriceModeChangedMessage(data.DefaultPriceMode));
            WeakReferenceMessenger.Default.Send(
                new DiscountBeforeTaxChangedMessage(data.DiscountBeforeTax));

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
    // TEST CONNEXION (inchangé)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task TestConnection()
    {
        IsBusy = true;
        IsTestingConnection = true;
        HasTestResult = false;
        ShowSaveSuccess = false;
        ShowSaveError = false;

        var sw = System.Diagnostics.Stopwatch.StartNew();

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
                    TestDetails = $"Mode : e-MCF (API REST)\n" +
                                  $"URL : {EmcfApiUrl}\n" +
                                  $"NIF : {CompanyNIF}\n" +
                                  $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                  $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"{{ \"success\": true, \"nim\": \"{status.NIM}\" }}";
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
                    TestDetails = $"Mode : e-MCF (API REST)\n" +
                                  $"URL : {EmcfApiUrl}\n" +
                                  $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                  $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"{{ \"success\": false, \"error\": \"{status.ErrorMessage}\" }}";
                }

                HasTestResult = true;
            }
            else
            {
                if (SelectedComPort == "(aucun port détecté)")
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
                    TestDetails = $"Mode : MCF (Port Série)\n" +
                                  $"Port : {SelectedComPort}\n" +
                                  $"Baud Rate : {BaudRate}\n" +
                                  $"NIF : {status.NIF}\n" +
                                  $"NIM : {status.NIM}\n" +
                                  $"Format : 8N1\n" +
                                  $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                  $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"NIM={status.NIM}, NIF={status.NIF}, Success=true";
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
                    TestDetails = $"Mode : MCF (Port Série)\n" +
                                  $"Port : {SelectedComPort}\n" +
                                  $"Baud Rate : {BaudRate}\n" +
                                  $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                  $"Temps de réponse : {sw.ElapsedMilliseconds} ms";
                    TestRawResponse = $"Error: {status.ErrorMessage}";
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
            TestDetails = $"Exception : {ex.GetType().Name}\n" +
                          $"Testé le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                          $"Temps : {sw.ElapsedMilliseconds} ms";
            TestRawResponse = ex.ToString();
            HasTestResult = true;
        }
        finally
        {
            IsBusy = false;
            IsTestingConnection = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LICENCE (inchangé)
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

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();

        // 🔑 Passer le CompanyId au sous-VM et charger les POS
        PosManagement.CompanyId = _companyId;
        await PosManagement.LoadCommand.ExecuteAsync(null);
    }

}