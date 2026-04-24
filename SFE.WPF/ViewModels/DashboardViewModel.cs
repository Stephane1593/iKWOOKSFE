using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SFE.Application.Services;
using SFE.Domain.Enums;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SFE.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly DashboardService _dashboardService;

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

    public DashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        PageTitle = "Tableau de bord";
        _ = LoadDashboardAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDashboardAsync();

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