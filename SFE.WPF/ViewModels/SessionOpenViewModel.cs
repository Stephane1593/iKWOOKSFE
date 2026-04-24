using System.Collections.ObjectModel;
using System.Globalization;
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

    // ═══ POINT OF SALE (single, from User.PointOfSaleId) ═══
    [ObservableProperty] private PointOfSale? _assignedPos;
    [ObservableProperty] private string _assignedPosDetail = "";

    // ═══ POS AVAILABILITY ═══
    [ObservableProperty] private bool _hasPosAvailable;
    [ObservableProperty] private bool _noPosAvailable;
    [ObservableProperty] private string _noPosMessage = "";
    [ObservableProperty] private string _noPosIcon = "⚠";

    // ═══ IT TECH BYPASS ═══
    [ObservableProperty] private bool _canBypassPosCheck;
    [ObservableProperty] private bool _showNoPosBlocker;
    [ObservableProperty] private bool _showItTechBypass;

    // ═══ EXCHANGE RATES ═══
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

    public SessionOpenViewModel(IUnitOfWork unitOfWork, IAuthService authService, SettingsService settingsService)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _settingsService = settingsService;

        var now = DateTime.Now;
        CurrentDate = now.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));
        CurrentTime = now.ToString("HH:mm");
        OperatorName = authService.CurrentUser?.FullName ?? "Opérateur";

        CanBypassPosCheck = authService.HasPermission("bypassPosCheck");

        _ = LoadAsync();
    }

    // ═══════════════════════════════════════════
    //  LOAD
    // ═══════════════════════════════════════════

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await LoadPointOfSaleAsync();
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

    private async Task LoadPointOfSaleAsync()
    {
        NoPosAvailable = false;
        HasPosAvailable = false;
        ShowNoPosBlocker = false;
        ShowItTechBypass = false;

        var user = _authService.CurrentUser;
        if (user == null)
        {
            SetNoPosState("Aucun utilisateur connecté. Veuillez vous reconnecter.", "🔒");
            return;
        }

        // ── User has no POS assigned ──
        if (!user.PointOfSaleId.HasValue)
        {
            SetNoPosState(
                "Aucun point de vente ne vous est assigné.\nContactez votre administrateur pour obtenir l'accès.",
                "🚫");
            return;
        }

        // ── Load the assigned POS ──
        var pos = user.PointOfSale
                  ?? await _unitOfWork.PointsOfSale.GetByIdAsync(user.PointOfSaleId.Value);

        if (pos == null)
        {
            SetNoPosState(
                "Le point de vente assigné est introuvable.\nContactez votre administrateur.",
                "⚠");
            return;
        }

        if (!pos.IsActive)
        {
            SetNoPosState(
                $"Le point de vente « {pos.Code} — {pos.Name} » est désactivé.\nContactez votre administrateur.",
                "⚠");
            return;
        }

        // ── Success ──
        AssignedPos = pos;
        HasPosAvailable = true;
        NoPosAvailable = false;
        ShowNoPosBlocker = false;
        ShowItTechBypass = false;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(pos.City)) parts.Add(pos.City);
        parts.Add(pos.DeviceType.ToString());
        if (!string.IsNullOrWhiteSpace(pos.EmcfNIM)) parts.Add($"NIM : {pos.EmcfNIM}");
        AssignedPosDetail = string.Join(" · ", parts);
    }

    private void SetNoPosState(string message, string icon = "⚠")
    {
        NoPosAvailable = true;
        HasPosAvailable = false;
        NoPosMessage = message;
        NoPosIcon = icon;
        AssignedPos = null;

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
        catch { }
    }

    // ═══════════════════════════════════════════
    //  RECALCULATION (unchanged)
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

        if (NoPosAvailable)
        {
            ErrorMessage = "Impossible d'ouvrir une session : aucun point de vente disponible.";
            HasError = true;
            return;
        }

        if (AssignedPos == null)
        {
            ErrorMessage = "Point de vente introuvable.";
            HasError = true;
            return;
        }

        if (!decimal.TryParse(RateUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var rUsd) || rUsd <= 0)
        {
            ErrorMessage = "Le taux de change USD → CDF est obligatoire et doit être supérieur à zéro.";
            HasError = true;
            return;
        }

        decimal.TryParse(RateEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var rEur);
        decimal.TryParse(RateCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var rCny);
        decimal.TryParse(AmountUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var aUsd);
        decimal.TryParse(AmountCDF, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCdf);
        decimal.TryParse(AmountEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var aEur);
        decimal.TryParse(AmountCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var aCny);

        if (aUsd < 0 || aCdf < 0 || aEur < 0 || aCny < 0)
        {
            ErrorMessage = "Les montants ne peuvent pas être négatifs.";
            HasError = true;
            return;
        }

        IsBypass = false;
        Result = new CashSessionInfo
        {
            OpenedAt = DateTime.Now,
            OperatorName = OperatorName,
            PointOfSaleId = AssignedPos.Id,
            PointOfSaleName = AssignedPos.Name,
            PointOfSaleCode = AssignedPos.Code,
            PointOfSaleCity = AssignedPos.City,
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