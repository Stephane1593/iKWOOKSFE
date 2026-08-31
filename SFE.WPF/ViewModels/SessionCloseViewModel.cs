using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public partial class SessionCloseViewModel : ObservableObject
{
    private readonly ReportService _reportService;
    private readonly CashSessionState _sessionState;
    private readonly ITimeProvider _timeProvider;     // 🆕

    // ═══ SESSION INFO (read-only) ═══
    [ObservableProperty] private string _operatorName = "";
    [ObservableProperty] private string _posName = "";
    [ObservableProperty] private string _posCode = "";
    [ObservableProperty] private string _openedAtDisplay = "";
    [ObservableProperty] private string _durationDisplay = "";
    [ObservableProperty] private string _currentDate = "";
    [ObservableProperty] private string _currentTime = "";

    // ═══ OPENING AMOUNTS ═══
    [ObservableProperty] private decimal _openingUSD;
    [ObservableProperty] private decimal _openingCDF;
    [ObservableProperty] private decimal _openingEUR;
    [ObservableProperty] private decimal _openingCNY;
    [ObservableProperty] private string _openingTotalCDF = "0";

    // ═══ EXCHANGE RATES ═══
    [ObservableProperty] private decimal _rateUSD;
    [ObservableProperty] private decimal _rateEUR;
    [ObservableProperty] private decimal _rateCNY;

    // ═══ SALES SUMMARY ═══
    [ObservableProperty] private int _totalInvoiceCount;
    [ObservableProperty] private int _salesCount;
    [ObservableProperty] private int _creditNoteCount;
    [ObservableProperty] private string _netTTCDisplay = "0";
    [ObservableProperty] private int _incompleteCount;
    [ObservableProperty] private string _nonCashTotalDisplay = "0";

    // ═══ CASH SALES DETAIL ═══
    [ObservableProperty] private string _cashSalesUSD = "0";
    [ObservableProperty] private string _cashSalesCDF = "0";
    [ObservableProperty] private string _cashSalesEUR = "0";
    [ObservableProperty] private string _cashSalesCNY = "0";

    // ═══ EXPECTED CASH ═══
    [ObservableProperty] private string _expectedUSD = "0";
    [ObservableProperty] private string _expectedCDF = "0";
    [ObservableProperty] private string _expectedEUR = "0";
    [ObservableProperty] private string _expectedCNY = "0";
    [ObservableProperty] private string _expectedTotalCDF = "0";

    private decimal _expectedUSDVal, _expectedCDFVal, _expectedEURVal, _expectedCNYVal;

    // ═══ CLOSING AMOUNTS ═══
    [ObservableProperty] private string _closingAmountUSD = "0";
    [ObservableProperty] private string _closingAmountCDF = "0";
    [ObservableProperty] private string _closingAmountEUR = "0";
    [ObservableProperty] private string _closingAmountCNY = "0";
    [ObservableProperty] private string _closingTotalCDF = "0";

    // ═══ VARIANCE ═══
    [ObservableProperty] private string _varianceUSD = "0";
    [ObservableProperty] private string _varianceCDF = "0";
    [ObservableProperty] private string _varianceEUR = "0";
    [ObservableProperty] private string _varianceCNY = "0";
    [ObservableProperty] private string _varianceTotalCDF = "0";
    [ObservableProperty] private string _varianceStatus = "";
    [ObservableProperty] private bool _hasVariance;
    [ObservableProperty] private bool _isPositiveVariance;

    // ═══ NOTES ═══
    [ObservableProperty] private string _closingNotes = "";

    // ═══ STATUS ═══
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _successMessage = "";
    [ObservableProperty] private bool _hasSuccess;
    [ObservableProperty] private bool _isDataLoaded;

    // ═══ RESULT ═══
    public DailyReport? GeneratedReport { get; private set; }
    public event Action? SessionClosed;

    // ═══════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════

    public SessionCloseViewModel(
        ReportService reportService,
        CashSessionState sessionState,
        ITimeProvider timeProvider)                  // 🆕
    {
        _reportService = reportService;
        _sessionState = sessionState;
        _timeProvider = timeProvider;                // 🆕

        // 🆕 Header clock via provider
        var localNow = _timeProvider.LocalNow;
        CurrentDate = localNow.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));
        CurrentTime = localNow.ToString("HH:mm");

        LoadSessionInfo();
        _ = LoadSalesSummaryAsync();
    }

    // ═══════════════════════════════════════════
    //  LOAD SESSION INFO
    // ═══════════════════════════════════════════

    private void LoadSessionInfo()
    {
        var session = _sessionState.Current;
        if (session == null)
        {
            ErrorMessage = "Aucune session active.";
            HasError = true;
            return;
        }

        OperatorName = session.OperatorName;
        PosName = session.PointOfSaleName;
        PosCode = session.PointOfSaleCode;

        // 🆕 session.OpenedAt is DateTimeOffset (UTC). Project to local for display.
        var openedLocal = _timeProvider.ToLocal(session.OpenedAt);
        OpenedAtDisplay = openedLocal.ToString("dd/MM/yyyy HH:mm");

        // 🆕 Duration: compare two DateTimeOffset values directly — they know their offsets.
        var duration = _timeProvider.LocalNow - session.OpenedAt;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;    // safety
        DurationDisplay = $"{(int)duration.TotalHours}h {duration.Minutes:D2}min";

        OpeningUSD = session.OpeningAmountUSD;
        OpeningCDF = session.OpeningAmountCDF;
        OpeningEUR = session.OpeningAmountEUR;
        OpeningCNY = session.OpeningAmountCNY;
        OpeningTotalCDF = session.TotalEquivalentCDF.ToString("N0");

        RateUSD = session.RateUSD;
        RateEUR = session.RateEUR;
        RateCNY = session.RateCNY;

        ClosingAmountUSD = session.OpeningAmountUSD.ToString("F2");
        ClosingAmountCDF = session.OpeningAmountCDF.ToString("F0");
        ClosingAmountEUR = session.OpeningAmountEUR.ToString("F2");
        ClosingAmountCNY = session.OpeningAmountCNY.ToString("F2");
    }

    // ═══════════════════════════════════════════
    //  LOAD SALES SUMMARY
    // ═══════════════════════════════════════════

    private async Task LoadSalesSummaryAsync()
    {
        var session = _sessionState.Current;
        if (session == null) return;

        IsLoading = true;
        try
        {
            // 🆕 Pass the DateTimeOffset straight through.
            var summary = await _reportService.CalculateSessionSummaryAsync(
                session.OpenedAt,
                session.PointOfSaleId,
                session.OpeningAmountUSD,
                session.OpeningAmountCDF,
                session.OpeningAmountEUR,
                session.OpeningAmountCNY);

            TotalInvoiceCount = summary.TotalInvoiceCount;
            SalesCount = summary.SalesCount;
            CreditNoteCount = summary.CreditNoteCount;
            NetTTCDisplay = summary.NetTTC.ToString("N0");
            IncompleteCount = summary.IncompleteCount;
            NonCashTotalDisplay = summary.NonCashTotal.ToString("N0");

            CashSalesUSD = FormatCashFlow(summary.CashSalesUSD, summary.CashRefundsUSD);
            CashSalesCDF = FormatCashFlow(summary.CashSalesCDF, summary.CashRefundsCDF);
            CashSalesEUR = FormatCashFlow(summary.CashSalesEUR, summary.CashRefundsEUR);
            CashSalesCNY = FormatCashFlow(summary.CashSalesCNY, summary.CashRefundsCNY);

            _expectedUSDVal = summary.ExpectedCashUSD;
            _expectedCDFVal = summary.ExpectedCashCDF;
            _expectedEURVal = summary.ExpectedCashEUR;
            _expectedCNYVal = summary.ExpectedCashCNY;

            ExpectedUSD = summary.ExpectedCashUSD.ToString("N2");
            ExpectedCDF = summary.ExpectedCashCDF.ToString("N0");
            ExpectedEUR = summary.ExpectedCashEUR.ToString("N2");
            ExpectedCNY = summary.ExpectedCashCNY.ToString("N2");

            var expectedTotal = (summary.ExpectedCashUSD * RateUSD)
                              + summary.ExpectedCashCDF
                              + (summary.ExpectedCashEUR * RateEUR)
                              + (summary.ExpectedCashCNY * RateCNY);
            ExpectedTotalCDF = expectedTotal.ToString("N0");

            IsDataLoaded = true;
            RecalculateVariance();
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

    private static string FormatCashFlow(decimal sales, decimal refunds)
    {
        if (refunds > 0)
            return $"+{sales:N2} −{refunds:N2}";
        return sales > 0 ? $"+{sales:N2}" : "0";
    }

    // ═══════════════════════════════════════════
    //  RECALCULATE VARIANCE
    // ═══════════════════════════════════════════

    partial void OnClosingAmountUSDChanged(string value) => RecalculateVariance();
    partial void OnClosingAmountCDFChanged(string value) => RecalculateVariance();
    partial void OnClosingAmountEURChanged(string value) => RecalculateVariance();
    partial void OnClosingAmountCNYChanged(string value) => RecalculateVariance();

    private void RecalculateVariance()
    {
        if (!IsDataLoaded) return;

        decimal.TryParse(ClosingAmountUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var cUSD);
        decimal.TryParse(ClosingAmountCDF, NumberStyles.Any, CultureInfo.InvariantCulture, out var cCDF);
        decimal.TryParse(ClosingAmountEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var cEUR);
        decimal.TryParse(ClosingAmountCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var cCNY);

        var closingTotal = (cUSD * RateUSD) + cCDF + (cEUR * RateEUR) + (cCNY * RateCNY);
        ClosingTotalCDF = closingTotal.ToString("N0");

        var vUSD = cUSD - _expectedUSDVal;
        var vCDF = cCDF - _expectedCDFVal;
        var vEUR = cEUR - _expectedEURVal;
        var vCNY = cCNY - _expectedCNYVal;

        VarianceUSD = FormatVariance(vUSD);
        VarianceCDF = FormatVariance(vCDF);
        VarianceEUR = FormatVariance(vEUR);
        VarianceCNY = FormatVariance(vCNY);

        var vTotal = (vUSD * RateUSD) + vCDF + (vEUR * RateEUR) + (vCNY * RateCNY);
        VarianceTotalCDF = FormatVariance(vTotal);

        HasVariance = vTotal != 0;
        IsPositiveVariance = vTotal > 0;

        if (vTotal == 0)
            VarianceStatus = "✓ Caisse équilibrée";
        else if (vTotal > 0)
            VarianceStatus = $"⚠ Excédent de {vTotal:N0} CDF";
        else
            VarianceStatus = $"⚠ Manquant de {Math.Abs(vTotal):N0} CDF";
    }

    private static string FormatVariance(decimal v) => v switch
    {
        > 0 => $"+{v:N2}",
        < 0 => $"{v:N2}",
        _ => "0"
    };

    // ═══════════════════════════════════════════
    //  SET EXPECTED
    // ═══════════════════════════════════════════

    [RelayCommand]
    private void FillExpectedAmounts()
    {
        ClosingAmountUSD = _expectedUSDVal.ToString("F2");
        ClosingAmountCDF = _expectedCDFVal.ToString("F0");
        ClosingAmountEUR = _expectedEURVal.ToString("F2");
        ClosingAmountCNY = _expectedCNYVal.ToString("F2");
    }

    // ═══════════════════════════════════════════
    //  CONFIRM
    // ═══════════════════════════════════════════

    [RelayCommand]
    private async Task Confirm()
    {
        var session = _sessionState.Current;
        if (session == null)
        {
            ErrorMessage = "Session introuvable.";
            HasError = true;
            return;
        }

        HasError = false;
        HasSuccess = false;
        IsBusy = true;

        try
        {
            decimal.TryParse(ClosingAmountUSD, NumberStyles.Any, CultureInfo.InvariantCulture, out var cUSD);
            decimal.TryParse(ClosingAmountCDF, NumberStyles.Any, CultureInfo.InvariantCulture, out var cCDF);
            decimal.TryParse(ClosingAmountEUR, NumberStyles.Any, CultureInfo.InvariantCulture, out var cEUR);
            decimal.TryParse(ClosingAmountCNY, NumberStyles.Any, CultureInfo.InvariantCulture, out var cCNY);

            if (cUSD < 0 || cCDF < 0 || cEUR < 0 || cCNY < 0)
            {
                ErrorMessage = "Les montants de clôture ne peuvent pas être négatifs.";
                HasError = true;
                IsBusy = false;
                return;
            }

            var closeData = new SessionCloseData
            {
                SessionOpenedAt = session.OpenedAt,        // already DateTimeOffset
                PointOfSaleId = session.PointOfSaleId,
                OperatorName = session.OperatorName,

                OpeningAmountUSD = session.OpeningAmountUSD,
                OpeningAmountCDF = session.OpeningAmountCDF,
                OpeningAmountEUR = session.OpeningAmountEUR,
                OpeningAmountCNY = session.OpeningAmountCNY,

                RateUSD = session.RateUSD,
                RateEUR = session.RateEUR,
                RateCNY = session.RateCNY,

                OpeningNotes = session.Notes,

                ClosingAmountUSD = cUSD,
                ClosingAmountCDF = cCDF,
                ClosingAmountEUR = cEUR,
                ClosingAmountCNY = cCNY,

                ClosingNotes = string.IsNullOrWhiteSpace(ClosingNotes) ? null : ClosingNotes.Trim()
            };

            GeneratedReport = await _reportService.GenerateSessionZReportAsync(closeData);

            _sessionState.Close();

            SuccessMessage = $"✓ Z-Rapport N°{GeneratedReport.ReportNumber} généré avec succès.";
            HasSuccess = true;

            SessionClosed?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur lors de la clôture : {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event Action? CloseRequested;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}