using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.WPF.Models;

namespace SFE.WPF.ViewModels;

public partial class SessionOpenViewModel : ObservableObject
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly SettingsService _settingsService;

    // ═══ HEADER ═══
    [ObservableProperty] private string _currentDate = "";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _operatorName = "";

    // ═══ POINT OF SALE ═══
    public ObservableCollection<PointOfSale> AvailablePos { get; } = new();
    [ObservableProperty] private PointOfSale? _selectedPos;
    [ObservableProperty] private bool _hasMultiplePos;
    [ObservableProperty] private string _selectedPosDetail = "";

    // ═══ POS AVAILABILITY ═══
    [ObservableProperty] private bool _hasPosAvailable;
    [ObservableProperty] private bool _noPosAvailable;
    [ObservableProperty] private string _noPosMessage = "";
    [ObservableProperty] private string _noPosIcon = "⚠";

    // ═══ IT TECH BYPASS ═══
    [ObservableProperty] private bool _canBypassPosCheck;
    [ObservableProperty] private bool _showNoPosBlocker;   // regular user blocked
    [ObservableProperty] private bool _showItTechBypass;    // IT Tech can skip

    // ═══ EXCHANGE RATES (string for binding) ═══
    [ObservableProperty] private string _rateUSD = "2800";
    [ObservableProperty] private string _rateEUR = "3024";
    [ObservableProperty] private string _rateCNY = "385";

    // ═══ OPENING AMOUNTS ═══
    [ObservableProperty] private string _amountUSD = "0";
    [ObservableProperty] private string _amountCDF = "0";
    [ObservableProperty] private string _amountEUR = "0";
    [ObservableProperty] private string _amountCNY = "0";

    // ═══ CDF EQUIVALENTS ═══
    [ObservableProperty] private string _equivUSD = "0";
    [ObservableProperty] private string _equivCDF = "0";
    [ObservableProperty] private string _equivEUR = "0";
    [ObservableProperty] private string _equivCNY = "0";
    [ObservableProperty] private string _totalEquivCDF = "0";
    [ObservableProperty] private decimal _totalEquivCDFValue;

    // ═══ NOTES ═══
    [ObservableProperty] private string _notes = "";

    // ═══ STATUS ═══
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isLoading;

    // ═══ RESULT ═══
    public CashSessionInfo? Result { get; private set; }
    public bool IsBypass { get; private set; }
    public event Action? SessionConfirmed;
    public event Action? SessionBypassed;

    // ═══════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════

    public SessionOpenViewModel(IUnitOfWork unitOfWork, IAuthService authService, SettingsService settingsService)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _settingsService = settingsService;

        var now = DateTime.Now;
        CurrentDate = now.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));
        CurrentTime = now.ToString("HH:mm");
        OperatorName = authService.CurrentUser?.FullName ?? "Opérateur";

        // ── Check bypass permission early ──
        CanBypassPosCheck = HasPermission("bypassPosCheck");

        _ = LoadAsync();
    }

    // ═══════════════════════════════════════════
    //  PERMISSION HELPER
    // ═══════════════════════════════════════════

    private bool HasPermission(string permissionKey)
    {
        var user = _authService.CurrentUser;
        if (user?.Role?.Permissions == null) return false;

        try
        {
            using var doc = JsonDocument.Parse(user.Role.Permissions);
            if (doc.RootElement.TryGetProperty(permissionKey, out var prop)
                && prop.ValueKind == JsonValueKind.True)
                return true;
        }
        catch
        {
            // Malformed JSON — fail safe
        }

        return false;
    }

    // ═══════════════════════════════════════════
    //  LOAD DATA
    // ═══════════════════════════════════════════

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await LoadPointsOfSaleAsync();
            await LoadLatestExchangeRateAsync();
            RecalculateEquivalents();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de chargement : {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPointsOfSaleAsync()
    {
        // ── Reset state ──
        NoPosAvailable = false;
        NoPosMessage = "";
        HasPosAvailable = false;
        ShowNoPosBlocker = false;
        ShowItTechBypass = false;

        // ── Check user ──
        var user = _authService.CurrentUser;
        if (user == null)
        {
            SetNoPosState("Aucun utilisateur connecté. Veuillez vous reconnecter.", "🔒");
            return;
        }

        // ── Check company ──
        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company == null)
        {
            SetNoPosState(
                "Aucune entreprise configurée.\nVeuillez d'abord créer votre entreprise dans les paramètres.",
                "🏢");
            return;
        }

        // ── Check company has POS ──
        var companyWithPos = await _unitOfWork.Companies.GetWithPointsOfSaleAsync(company.Id);
        if (companyWithPos?.PointsOfSale == null || !companyWithPos.PointsOfSale.Any())
        {
            SetNoPosState(
                "Aucun point de vente enregistré.\nVeuillez créer au moins un point de vente dans Paramètres → Points de vente.",
                "🏪");
            return;
        }

        // ── Check active POS ──
        var allActivePos = companyWithPos.PointsOfSale
            .Where(p => p.IsActive)
            .ToList();

        if (!allActivePos.Any())
        {
            SetNoPosState(
                $"{companyWithPos.PointsOfSale.Count} point(s) de vente trouvé(s) mais aucun n'est actif.\nActivez au moins un point de vente dans les paramètres.",
                "⚠");
            return;
        }

        // ── Filter by user assignment ──
        var assignedIds = JsonSerializer.Deserialize<int[]>(user.AssignedPosIds ?? "[]") ?? [];

        var accessiblePos = allActivePos
            .Where(p => assignedIds.Length == 0 || assignedIds.Contains(p.Id))
            .OrderBy(p => p.Code)
            .ToList();

        if (!accessiblePos.Any())
        {
            SetNoPosState(
                $"{allActivePos.Count} point(s) de vente actif(s) mais aucun ne vous est assigné.\nContactez votre administrateur pour obtenir l'accès.",
                "🚫");
            return;
        }

        // ── Success — populate list ──
        AvailablePos.Clear();
        foreach (var pos in accessiblePos)
            AvailablePos.Add(pos);

        HasMultiplePos = accessiblePos.Count > 1;
        HasPosAvailable = true;
        NoPosAvailable = false;
        ShowNoPosBlocker = false;
        ShowItTechBypass = false;
        SelectedPos = accessiblePos.FirstOrDefault();
    }

    /// <summary>
    /// Helper to set the "no POS" warning state consistently.
    /// Splits into blocker vs IT Tech bypass based on permission.
    /// </summary>
    private void SetNoPosState(string message, string icon = "⚠")
    {
        NoPosAvailable = true;
        HasPosAvailable = false;
        NoPosMessage = message;
        NoPosIcon = icon;
        HasMultiplePos = false;
        SelectedPos = null;
        AvailablePos.Clear();

        // ── Decide which panel to show ──
        if (CanBypassPosCheck)
        {
            ShowItTechBypass = true;
            ShowNoPosBlocker = false;
        }
        else
        {
            ShowItTechBypass = false;
            ShowNoPosBlocker = true;
        }
    }

    private async Task LoadLatestExchangeRateAsync()
    {
        try
        {
            var data = await _settingsService.LoadSettingsAsync();

            if (data.CurrentExchangeRate > 0)
                RateUSD = data.CurrentExchangeRate.ToString("F0");

            if (data.CurrentExchangeRateEUR > 0)
                RateEUR = data.CurrentExchangeRateEUR.ToString("F0");

            if (data.CurrentExchangeRateCNY > 0)
                RateCNY = data.CurrentExchangeRateCNY.ToString("F0");
        }
        catch
        {
            // Repository not available yet — keep defaults
        }
    }

    // ═══════════════════════════════════════════
    //  POS SELECTION
    // ═══════════════════════════════════════════

    partial void OnSelectedPosChanged(PointOfSale? value)
    {
        if (value == null) { SelectedPosDetail = ""; return; }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(value.City)) parts.Add(value.City);
        parts.Add(value.DeviceType.ToString());
        if (!string.IsNullOrWhiteSpace(value.EmcfNIM)) parts.Add($"NIM : {value.EmcfNIM}");

        SelectedPosDetail = string.Join(" · ", parts);
    }

    // ═══════════════════════════════════════════
    //  RECALCULATION
    // ═══════════════════════════════════════════

    partial void OnRateUSDChanged(string value) => RecalculateEquivalents();
    partial void OnRateEURChanged(string value) => RecalculateEquivalents();
    partial void OnRateCNYChanged(string value) => RecalculateEquivalents();
    partial void OnAmountUSDChanged(string value) => RecalculateEquivalents();
    partial void OnAmountCDFChanged(string value) => RecalculateEquivalents();
    partial void OnAmountEURChanged(string value) => RecalculateEquivalents();
    partial void OnAmountCNYChanged(string value) => RecalculateEquivalents();

    private void RecalculateEquivalents()
    {
        decimal.TryParse(RateUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var rUsd);
        decimal.TryParse(RateEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var rEur);
        decimal.TryParse(RateCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var rCny);

        decimal.TryParse(AmountUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var aUsd);
        decimal.TryParse(AmountCDF, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCdf);
        decimal.TryParse(AmountEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var aEur);
        decimal.TryParse(AmountCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCny);

        var eUsd = aUsd * rUsd;
        var eCdf = aCdf;
        var eEur = aEur * rEur;
        var eCny = aCny * rCny;

        EquivUSD = eUsd.ToString("N0");
        EquivCDF = eCdf.ToString("N0");
        EquivEUR = eEur.ToString("N0");
        EquivCNY = eCny.ToString("N0");

        var total = eUsd + eCdf + eEur + eCny;
        TotalEquivCDFValue = total;
        TotalEquivCDF = total.ToString("N0");
    }

    // ═══════════════════════════════════════════
    //  BYPASS (IT TECH)
    // ═══════════════════════════════════════════

    [RelayCommand]
    private void Bypass()
    {
        IsBypass = true;
        Result = null;
        SessionBypassed?.Invoke();
    }

    // ═══════════════════════════════════════════
    //  CONFIRM
    // ═══════════════════════════════════════════

    [RelayCommand]
    private void Confirm()
    {
        HasError = false;
        ErrorMessage = "";

        // ── Check POS availability first ──
        if (NoPosAvailable)
        {
            ErrorMessage = "Impossible d'ouvrir une session : aucun point de vente disponible.";
            HasError = true;
            return;
        }

        // ── Validate POS ──
        if (SelectedPos == null)
        {
            ErrorMessage = "Veuillez sélectionner un point de vente.";
            HasError = true;
            return;
        }

        // ── Validate exchange rate ──
        if (!decimal.TryParse(RateUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var rUsd)
            || rUsd <= 0)
        {
            ErrorMessage = "Le taux de change USD → CDF est obligatoire et doit être supérieur à zéro.";
            HasError = true;
            return;
        }

        // ── Parse all values ──
        decimal.TryParse(RateEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var rEur);
        decimal.TryParse(RateCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var rCny);
        decimal.TryParse(AmountUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var aUsd);
        decimal.TryParse(AmountCDF, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCdf);
        decimal.TryParse(AmountEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var aEur);
        decimal.TryParse(AmountCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCny);

        // ── Validate no negatives ──
        if (aUsd < 0 || aCdf < 0 || aEur < 0 || aCny < 0)
        {
            ErrorMessage = "Les montants ne peuvent pas être négatifs.";
            HasError = true;
            return;
        }

        // ── Build result ──
        IsBypass = false;
        Result = new CashSessionInfo
        {
            OpenedAt = DateTime.Now,
            OperatorName = OperatorName,
            PointOfSaleId = SelectedPos.Id,
            PointOfSaleName = SelectedPos.Name,
            PointOfSaleCode = SelectedPos.Code,
            PointOfSaleCity = SelectedPos.City,
            OpeningAmountUSD = aUsd,
            OpeningAmountCDF = aCdf,
            OpeningAmountEUR = aEur,
            OpeningAmountCNY = aCny,
            RateUSD = rUsd,
            RateEUR = rEur,
            RateCNY = rCny,
            Notes = Notes
        };

        SessionConfirmed?.Invoke();
    }
}