using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;   // DispatcherTimer
using SFE.Domain.Abstractions;    // ITimeProvider

namespace SFE.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly DashboardService _dashboardService;
    private readonly IFiscalDeviceService _fiscalDevice;

    private readonly ITimeProvider _time;
    private DispatcherTimer? _clockTimer;

    [ObservableProperty] private string _greetingText = "Bonjour";
    [ObservableProperty] private string _greetingSubtext = "Bonne journée de travail";
    [ObservableProperty] private string _greetingIconKey = "WeatherSunny"; // MaterialDesign PackIcon name
    [ObservableProperty] private string _fullDateLabel = "";

    [ObservableProperty] private int _daysUntilMonthEnd;
    [ObservableProperty] private string _monthEndLabel = "";
    [ObservableProperty] private string _fiscalReminder = "";

    [ObservableProperty]
    private ObservableCollection<CityTimeItem> _cityClocks = [];

    // ══════════ KPI ══════════
    [ObservableProperty] private string _todaySalesAmount = "0 CDF";
    [ObservableProperty] private string _todayInvoiceCount = "0";
    [ObservableProperty] private string _loyaltyMemberCount = "0";
    [ObservableProperty] private string _lowStockAlerts = "0";
    [ObservableProperty] private string _salesTrend = "—";
    [ObservableProperty] private bool _salesTrendPositive;
    [ObservableProperty] private string _weekSalesAmount = "0 CDF";
    [ObservableProperty] private string _monthSalesAmount = "0 CDF";
    [ObservableProperty] private string _monthInvoiceCount = "0";

    // ══════════ CHARTS ══════════
    [ObservableProperty] private ISeries[] _salesSeries = [];
    [ObservableProperty] private Axis[] _salesXAxes = [];
    [ObservableProperty] private Axis[] _salesYAxes = [];

    [ObservableProperty] private ISeries[] _paymentSeries = [];
    [ObservableProperty] private ISeries[] _invoiceTypeSeries = [];

    [ObservableProperty] private ISeries[] _topProductsSeries = [];
    [ObservableProperty] private Axis[] _topProductsXAxes = [];
    [ObservableProperty] private Axis[] _topProductsYAxes = [];

    // ══════════ EMPTY STATE FLAGS ══════════
    [ObservableProperty] private bool _hasSalesData;
    [ObservableProperty] private bool _hasPaymentData;
    [ObservableProperty] private bool _hasInvoiceTypeData;
    [ObservableProperty] private bool _hasTopProductsData;
    [ObservableProperty] private bool _hasRecentData;

    // ══════════ RECENT ACTIVITY ══════════
    [ObservableProperty]
    private ObservableCollection<RecentInvoiceItem> _recentInvoices = [];

    // ══════════ FISCAL DEVICE STATUS (NEW) ══════════
    [ObservableProperty] private bool _isFiscalLoading;
    [ObservableProperty] private bool _fiscalConnected;
    [ObservableProperty] private string _fiscalDeviceType = "—";
    [ObservableProperty] private string _fiscalNIM = "—";
    [ObservableProperty] private string _fiscalNIF = "—";
    [ObservableProperty] private string _fiscalConnectionStatus = "DIS";
    [ObservableProperty] private string _fiscalConnectionLabel = "Déconnecté";
    [ObservableProperty] private string _fiscalLastSync = "—";
    [ObservableProperty] private string _fiscalPendingCount = "0";
    [ObservableProperty] private string _fiscalTotalTransactions = "0";
    [ObservableProperty] private string _fiscalLastInvoice = "—";
    [ObservableProperty] private string _fiscalTaxpayerName = "—";
    [ObservableProperty] private string _fiscalErrorMessage = "";
    [ObservableProperty] private bool _hasFiscalError;

    // ══════════ CHART PALETTE ══════════
    private static readonly SKColor[] Palette =
    [
        SKColor.Parse("#37B1E4"),
        SKColor.Parse("#10B981"),
        SKColor.Parse("#F59E0B"),
        SKColor.Parse("#8B5CF6"),
        SKColor.Parse("#EC4899"),
        SKColor.Parse("#EF4444"),
        SKColor.Parse("#06B6D4"),
    ];

    private static readonly Dictionary<InvoiceType, SKColor> TypeColors = new()
    {
        { InvoiceType.FV, SKColor.Parse("#37B1E4") },
        { InvoiceType.FT, SKColor.Parse("#8B5CF6") },
        { InvoiceType.FA, SKColor.Parse("#EF4444") },
        { InvoiceType.EV, SKColor.Parse("#10B981") },
        { InvoiceType.ET, SKColor.Parse("#F59E0B") },
        { InvoiceType.EA, SKColor.Parse("#EC4899") },
    };

    public DashboardViewModel(
        DashboardService dashboardService,
        IFiscalDeviceService fiscalDevice,
        ITimeProvider time)                   // 🆕
    {
        _dashboardService = dashboardService;
        _fiscalDevice = fiscalDevice;
        _time = time;                      // 🆕

        PageTitle = "Tableau de bord";

        InitializeCityClocks();                        // 🆕
        StartClockTimer();                             // 🆕
        TickClock();                                   // 🆕 initial paint

        _ = LoadDashboardAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDashboardAsync();

    [RelayCommand]
    private async Task RefreshFiscalStatusAsync() => await LoadFiscalStatusAsync();

    private async Task LoadDashboardAsync()
    {
        IsBusy = true;
        try
        {
            var d = await _dashboardService.LoadDashboardAsync();

            // ── KPIs ──
            TodaySalesAmount = $"{d.TodaySalesAmount:N0} CDF";
            TodayInvoiceCount = d.TodayInvoiceCount.ToString();
            LoyaltyMemberCount = d.LoyaltyMembers.ToString();
            LowStockAlerts = d.LowStockAlerts.ToString();
            WeekSalesAmount = $"{d.WeekSalesAmount:N0} CDF";
            MonthSalesAmount = $"{d.MonthSalesAmount:N0} CDF";
            MonthInvoiceCount = d.MonthInvoiceCount.ToString();

            // ── Trend vs yesterday ──
            if (d.YesterdaySalesAmount > 0)
            {
                var pct = ((d.TodaySalesAmount - d.YesterdaySalesAmount)
                           / d.YesterdaySalesAmount) * 100;
                SalesTrendPositive = pct >= 0;
                SalesTrend = $"{(pct >= 0 ? "▲ +" : "▼ ")}{pct:N1}% vs hier";
            }
            else
            {
                SalesTrendPositive = d.TodaySalesAmount > 0;
                SalesTrend = d.TodaySalesAmount > 0 ? "▲ Première vente" : "—";
            }

            // ── Build charts with empty-state awareness ──
            bool anySales = d.Last7DaysSales.Any(p => p.Amount != 0);
            HasSalesData = anySales;
            if (anySales) BuildSalesChart(d.Last7DaysSales);
            else SalesSeries = [];

            HasPaymentData = d.PaymentBreakdown.Count > 0;
            if (HasPaymentData) BuildPaymentChart(d.PaymentBreakdown);
            else PaymentSeries = [];

            HasInvoiceTypeData = d.InvoiceTypeBreakdown.Count > 0;
            if (HasInvoiceTypeData) BuildInvoiceTypeChart(d.InvoiceTypeBreakdown);
            else InvoiceTypeSeries = [];

            HasTopProductsData = d.TopProducts.Count > 0;
            if (HasTopProductsData) BuildTopProductsChart(d.TopProducts);
            else TopProductsSeries = [];

            // ── Recent ──
            RecentInvoices = new ObservableCollection<RecentInvoiceItem>(d.RecentInvoices);
            HasRecentData = d.RecentInvoices.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ DASHBOARD VM EXCEPTION: {ex}");
            System.Windows.MessageBox.Show(
                $"Erreur dashboard:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "Erreur Debug");
        }
        finally { IsBusy = false; }

        // Load fiscal status in parallel (non-blocking)
        _ = LoadFiscalStatusAsync();
    }

    // ══════════════════════════════════════════════════════════════
    // FISCAL DEVICE STATUS — Async load
    // ══════════════════════════════════════════════════════════════

    private async Task LoadFiscalStatusAsync()
    {
        IsFiscalLoading = true;
        HasFiscalError = false;
        FiscalErrorMessage = "";

        try
        {
            var info = await _fiscalDevice.GetDetailedInfoAsync();

            FiscalConnected = info.Success;
            FiscalDeviceType = info.DeviceTypeLabel;
            FiscalNIM = info.NIM ?? "—";
            FiscalNIF = info.NIF ?? "—";
            FiscalConnectionStatus = info.ConnectionStatus ?? "DIS";
            FiscalTaxpayerName = info.TaxpayerName ?? "—";
            FiscalTotalTransactions = info.TotalTransactions.ToString("N0");
            FiscalPendingCount = info.PendingRequestsCount > 0
                ? info.PendingRequestsCount.ToString()
                : (info.TransactionsInDevice > 0 ? info.TransactionsInDevice.ToString() : "0");

            FiscalConnectionLabel = info.ConnectionStatus switch
            {
                "CON" => "Connecté",
                "TRA" => "Transmission...",
                "RES" => "Restauration...",
                _ => "Déconnecté"
            };

            FiscalLastSync = info.LastServerConnection.HasValue
                ? info.LastServerConnection.Value.ToString("dd/MM/yyyy HH:mm")
                : "—";

            if (info.LastInvoiceDate.HasValue)
            {
                FiscalLastInvoice = $"{info.LastInvoiceType ?? "?"} " +
                    $"{info.LastInvoiceNumber ?? ""} — " +
                    $"{info.LastInvoiceDate.Value:dd/MM HH:mm}";
            }
            else
            {
                FiscalLastInvoice = "—";
            }

            if (!info.Success && !string.IsNullOrEmpty(info.ErrorMessage))
            {
                HasFiscalError = true;
                FiscalErrorMessage = info.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            FiscalConnected = false;
            FiscalConnectionStatus = "DIS";
            FiscalConnectionLabel = "Erreur";
            HasFiscalError = true;
            FiscalErrorMessage = ex.Message;
            Debug.WriteLine($"[Dashboard] Fiscal status error: {ex.Message}");
        }
        finally
        {
            IsFiscalLoading = false;
        }


    }

    // ══════════════════════════════════════════════════════════════
    // WORLD CLOCK — DRC cities (UTC+1 Kinshasa / UTC+2 Lubumbashi)
    // ══════════════════════════════════════════════════════════════

    private void InitializeCityClocks()
    {
        // DRC is split across two fixed offsets — no DST observed.
        var tzWest = TimeSpan.FromHours(1); // W. Central Africa Standard Time
        var tzEast = TimeSpan.FromHours(2); // South Africa Standard Time

        CityClocks =
        [
            new CityTimeItem { CityName = "Kinshasa",   Region = "Capitale",        UtcOffset = tzWest },
            new CityTimeItem { CityName = "Matadi",     Region = "Kongo-Central",   UtcOffset = tzWest },
            new CityTimeItem { CityName = "Lubumbashi", Region = "Haut-Katanga",    UtcOffset = tzEast },
            new CityTimeItem { CityName = "Goma",       Region = "Nord-Kivu",       UtcOffset = tzEast },
            new CityTimeItem { CityName = "Bukavu",     Region = "Sud-Kivu",        UtcOffset = tzEast },
            new CityTimeItem { CityName = "Kisangani",  Region = "Tshopo",          UtcOffset = tzEast },
        ];
    }

    private void StartClockTimer()
    {
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => TickClock();
        _clockTimer.Start();
    }

    private void TickClock()
    {
        var utcNow = _time.UtcNow;

        // Per-city tick
        foreach (var c in CityClocks)
            c.Update(utcNow);

        // Banner — use Kinshasa (capital) as the reference wall clock
        var kinshasa = utcNow.ToOffset(TimeSpan.FromHours(1));
        var hour = kinshasa.Hour;

        (GreetingText, GreetingSubtext, GreetingIconKey) = hour switch
        {
            >= 5 and < 12 => ("Bonjour", "Bonne journée de travail", "WeatherSunsetUp"),
            >= 12 and < 17 => ("Bon après-midi", "L'activité bat son plein", "WeatherSunny"),
            >= 17 and < 21 => ("Bonsoir", "Fin de journée en vue", "WeatherSunset"),
            _ => ("Bonne soirée", "Hors des heures d'ouverture", "WeatherNight")
        };

        // French long-form date: "vendredi 8 mai 2026"
        var frFR = CultureInfo.GetCultureInfo("fr-FR");
        FullDateLabel = char.ToUpper(kinshasa.ToString("dddd", frFR)[0])
                      + kinshasa.ToString("dddd d MMMM yyyy", frFR)[1..];

        // Fiscal countdown — DRC DGI monthly declarations are due by the 15th
        // of the following month. We surface "days until month-end" which is
        // the internal cutoff most accounting teams track.
        var todayLocal = DateOnly.FromDateTime(kinshasa.DateTime);
        var lastDayOfMonth = new DateOnly(
            todayLocal.Year, todayLocal.Month,
            DateTime.DaysInMonth(todayLocal.Year, todayLocal.Month));

        DaysUntilMonthEnd = lastDayOfMonth.DayNumber - todayLocal.DayNumber;
        MonthEndLabel = lastDayOfMonth.ToString("dd MMM", frFR);

        FiscalReminder = DaysUntilMonthEnd switch
        {
            0 => "🔔 Dernier jour du mois — clôture aujourd'hui",
            1 => "⚠️ Clôture mensuelle demain",
            <= 3 => $"📅 Clôture dans {DaysUntilMonthEnd} jours — préparez vos déclarations",
            <= 7 => $"📅 Fin du mois dans {DaysUntilMonthEnd} jours",
            _ => $"📅 {DaysUntilMonthEnd} jours avant fin de mois"
        };
    }

    /// <summary>
    /// Call from the page's Unloaded event to stop the clock timer
    /// when navigating away from the dashboard.
    /// </summary>
    public void StopClock()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
    }

    // ────────────────────────────────────────────────
    //  BAR CHART — 7-day sales
    // ────────────────────────────────────────────────
    private void BuildSalesChart(List<DailySalesPoint> pts)
    {
        SalesSeries =
        [
            new ColumnSeries<double>
            {
                Name        = "Ventes nettes",
                Values      = pts.Select(p => (double)p.Amount).ToArray(),
                Fill        = new SolidColorPaint(SKColor.Parse("#37B1E4")),
                Stroke      = null,
                Rx          = 5,
                Ry          = 5,
                MaxBarWidth = 38,
                Padding     = 8
            }
        ];

        SalesXAxes =
        [
            new Axis
            {
                Labels          = pts.Select(p => p.Date.ToString("ddd dd")).ToArray(),
                LabelsPaint     = new SolidColorPaint(SKColor.Parse("#4A6B7C")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E8F1F7")) { StrokeThickness = 1 },
                TextSize        = 12
            }
        ];

        SalesYAxes =
        [
            new Axis
            {
                LabelsPaint     = new SolidColorPaint(SKColor.Parse("#8BAAB9")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E8F1F7")) { StrokeThickness = 1 },
                TextSize        = 11,
                MinLimit        = 0,
                Labeler         = v => v switch
                {
                    >= 1_000_000 => $"{v / 1_000_000:N1}M",
                    >= 1_000     => $"{v / 1_000:N0}K",
                    _            => $"{v:N0}"
                }
            }
        ];
    }

    // ────────────────────────────────────────────────
    //  DONUT — Payment breakdown
    // ────────────────────────────────────────────────
    private void BuildPaymentChart(List<PaymentBreakdownItem> items)
    {
        PaymentSeries = items.Select((item, idx) =>
            new PieSeries<double>
            {
                Values = [(double)item.Total],
                Name = item.Label,
                Fill = new SolidColorPaint(Palette[idx % Palette.Length]),
                Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                Pushout = 0,
                InnerRadius = 50,
            } as ISeries
        ).ToArray();
    }

    // ────────────────────────────────────────────────
    //  DONUT — Invoice types
    // ────────────────────────────────────────────────
    private void BuildInvoiceTypeChart(List<InvoiceTypeBreakdownItem> items)
    {
        InvoiceTypeSeries = items.Select(item =>
            new PieSeries<double>
            {
                Values = [(double)item.Count],
                Name = item.Type.ToString(),
                Fill = new SolidColorPaint(
                                  TypeColors.GetValueOrDefault(item.Type, SKColor.Parse("#94A3B8"))),
                Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                Pushout = 0,
                InnerRadius = 45,
            } as ISeries
        ).ToArray();
    }

    // ────────────────────────────────────────────────
    //  HORIZONTAL BAR — Top 5 products
    // ────────────────────────────────────────────────
    private void BuildTopProductsChart(List<TopProductItem> items)
    {
        var reversed = items.AsEnumerable().Reverse().ToList();

        TopProductsSeries =
        [
            new RowSeries<double>
            {
                Name               = "Chiffre d'affaires",
                Values             = reversed.Select(p => (double)p.Revenue).ToArray(),
                Fill               = new SolidColorPaint(SKColor.Parse("#37B1E4")),
                Stroke             = null,
                MaxBarWidth        = 26,
                Rx                 = 4,
                Ry                 = 4,
                Padding            = 6,
                DataLabelsPaint    = new SolidColorPaint(SKColor.Parse("#4A6B7C")),
                DataLabelsSize     = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue:N0}"
            }
        ];

        TopProductsYAxes =
        [
            new Axis
            {
                Labels          = reversed.Select(p =>
                    p.Name.Length > 20 ? p.Name[..17] + "..." : p.Name).ToArray(),
                LabelsPaint     = new SolidColorPaint(SKColor.Parse("#4A6B7C")),
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
                TextSize        = 12
            }
        ];

        TopProductsXAxes =
        [
            new Axis
            {
                LabelsPaint     = new SolidColorPaint(SKColor.Parse("#8BAAB9")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E8F1F7")) { StrokeThickness = 1 },
                TextSize        = 11,
                MinLimit        = 0,
                Labeler         = v => v switch
                {
                    >= 1_000_000 => $"{v / 1_000_000:N1}M",
                    >= 1_000     => $"{v / 1_000:N0}K",
                    _            => $"{v:N0}"
                }
            }
        ];
    }
}