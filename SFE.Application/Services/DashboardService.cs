using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Enums;
using System.Diagnostics;

namespace SFE.Application.Services;

public class DashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimeProvider _time;

    public DashboardService(IUnitOfWork unitOfWork, ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<DashboardData> LoadDashboardAsync(int? activePosId = null)
    {
        // ══════════════════════════════════════════════════════
        //  "NOW" — DGI §1.1 single source of truth
        // ══════════════════════════════════════════════════════
        // For a DRC business, the "business day" is the LOCAL day
        // (Kinshasa UTC+1 or Lubumbashi UTC+2), not the UTC day.
        // Using UTC days would put late-night sales into the previous day.
        var nowLocal = _time.LocalNow;                            // DateTimeOffset
        var nowUtc = _time.UtcNow;                              // DateTimeOffset
        var todayLocal = DateOnly.FromDateTime(nowLocal.DateTime);

        // Start of local today, tagged with the local offset.
        // Used as the lower bound for time-range queries in UTC-equivalent form.
        var todayStartLocal = new DateTimeOffset(
            todayLocal.ToDateTime(TimeOnly.MinValue),
            nowLocal.Offset);

        Debug.WriteLine("╔══════════════════════════════════════════╗");
        Debug.WriteLine("║     DASHBOARD SERVICE - START            ║");
        Debug.WriteLine("╚══════════════════════════════════════════╝");
        Debug.WriteLine($"  activePosId = {activePosId?.ToString() ?? "NULL"}");
        Debug.WriteLine($"  nowLocal    = {nowLocal:O}  (offset {nowLocal.Offset})");
        Debug.WriteLine($"  nowUtc      = {nowUtc:O}");
        Debug.WriteLine($"  todayLocal  = {todayLocal:yyyy-MM-dd}");

        // ══════════════════════════════════════════════════════
        //  STEP 1: BASIC KPIs
        // ══════════════════════════════════════════════════════
        Debug.WriteLine("\n── STEP 1: Basic KPIs ──");

        var totalProducts = await _unitOfWork.Products.CountAsync();
        Debug.WriteLine($"  totalProducts = {totalProducts}");

        var totalClients = await _unitOfWork.Clients.CountAsync();
        Debug.WriteLine($"  totalClients = {totalClients}");

        var loyaltyMembers = (await _unitOfWork.Clients.GetLoyaltyMembersAsync()).Count;
        Debug.WriteLine($"  loyaltyMembers = {loyaltyMembers}");

        var todayCount = await _unitOfWork.Invoices.GetTodayCountAsync();
        Debug.WriteLine($"  todayCount (from repo) = {todayCount}");

        var todayTotal = await _unitOfWork.Invoices.GetTodayTotalAsync();
        Debug.WriteLine($"  todayTotal (from repo) = {todayTotal}");

        int lowStockAlerts;
        try
        {
            lowStockAlerts = activePosId is > 0
                ? await _unitOfWork.PosStocks.GetLowStockCountAsync(activePosId.Value)
                : await _unitOfWork.PosStocks.GetTotalLowStockCountAsync();
            Debug.WriteLine($"  lowStockAlerts = {lowStockAlerts}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  ❌ lowStockAlerts FAILED: {ex.Message}");
            lowStockAlerts = 0;
        }

        // ══════════════════════════════════════════════════════
        //  STEP 2: SEARCH INVOICES (last 30 local days)
        // ══════════════════════════════════════════════════════
        Debug.WriteLine("\n── STEP 2: Search invoices (last 30 local days, Normalized) ──");

        var thirtyDaysAgoLocal = todayStartLocal.AddDays(-30);
        Debug.WriteLine($"  DateFromLocal = {thirtyDaysAgoLocal:O}");
        Debug.WriteLine($"  DateToLocal   = {nowLocal:O}");
        Debug.WriteLine($"  Status        = {InvoiceStatus.Normalized}");

        var result = await _unitOfWork.Invoices.SearchAsync(
            new InvoiceSearchCriteria
            {
                // If SearchCriteria takes DateTime, wall-clock local is fine
                // because the repo compares it against invoice CreatedAt in the
                // same logical frame. If it's already DateTimeOffset, pass the
                // DTO directly and remove .DateTime.
                DateFrom = thirtyDaysAgoLocal.DateTime,
                DateTo = nowLocal.DateTime,
                Status = InvoiceStatus.Normalized
            }, 1, int.MaxValue);

        Debug.WriteLine($"  ✅ SearchAsync returned: {result.Items.Count} invoices");

        foreach (var inv in result.Items.Take(5))
        {
            Debug.WriteLine(
                $"    → {inv.InvoiceNumber} | " +
                $"LocalAtCreate={inv.CreatedAt.DateTime:yyyy-MM-dd HH:mm} | " +
                $"Offset={inv.CreatedAt.Offset} | " +
                $"Type={inv.Type} | TTC={inv.TotalTTC:N0} | " +
                $"PosId={inv.PointOfSaleId} | " +
                $"Lines={inv.Lines?.Count ?? -1} | " +
                $"Payments={inv.Payments?.Count ?? -1}");
        }

        // ══════════════════════════════════════════════════════
        //  STEP 3: POS FILTER
        // ══════════════════════════════════════════════════════
        var invoices = result.Items;
        if (activePosId is > 0)
        {
            var beforeFilter = invoices.Count;
            invoices = invoices.Where(i => i.PointOfSaleId == activePosId.Value).ToList();
            Debug.WriteLine($"\n── STEP 3: POS filter ──");
            Debug.WriteLine($"  Before: {beforeFilter} → After: {invoices.Count} " +
                            $"(filtering PosId={activePosId})");

            if (invoices.Count == 0 && beforeFilter > 0)
            {
                Debug.WriteLine($"  ⚠️ ALL INVOICES FILTERED OUT! " +
                    $"Invoice POS IDs: {string.Join(", ", result.Items.Select(i => i.PointOfSaleId).Distinct())}");
            }
        }
        else
        {
            Debug.WriteLine("\n── STEP 3: POS filter SKIPPED (no activePosId) ──");
        }

        // ══════════════════════════════════════════════════════
        //  STEP 4: Lines & Payments check
        // ══════════════════════════════════════════════════════
        Debug.WriteLine("\n── STEP 4: Lines & Payments check ──");
        var withLines = invoices.Count(i => i.Lines is { Count: > 0 });
        var withPayments = invoices.Count(i => i.Payments is { Count: > 0 });
        var nullLines = invoices.Count(i => i.Lines is null);
        var nullPayments = invoices.Count(i => i.Payments is null);
        Debug.WriteLine($"  Invoices with Lines:    {withLines} (null: {nullLines})");
        Debug.WriteLine($"  Invoices with Payments: {withPayments} (null: {nullPayments})");

        if (nullLines > 0)
            Debug.WriteLine($"  ⚠️ {nullLines} invoices have NULL Lines → TopProducts EMPTY!");
        if (nullPayments > 0)
            Debug.WriteLine($"  ⚠️ {nullPayments} invoices have NULL Payments → PaymentBreakdown EMPTY!");

        // ══════════════════════════════════════════════════════
        //  STEP 5: Type breakdown
        // ══════════════════════════════════════════════════════
        Debug.WriteLine("\n── STEP 5: Type breakdown ──");
        foreach (var g in invoices.GroupBy(i => i.Type))
            Debug.WriteLine($"    {g.Key}={g.Count()} " +
                            $"(IsSale={g.Key.IsSale()}, IsCreditNote={g.Key.IsCreditNote()})");

        var salesInvoices = invoices.Where(i => i.Type.IsSale()).ToList();
        Debug.WriteLine($"  Total sale invoices: {salesInvoices.Count}");

        // ══════════════════════════════════════════════════════
        //  STEP 6: Build charts
        // ══════════════════════════════════════════════════════
        Debug.WriteLine("\n── STEP 6: Building charts ──");

        // Bucketing helper: the invoice's own local calendar day.
        // DateTimeOffset.DateTime = wall clock at the stored offset,
        // so a sale stamped 2026-05-08 00:30 +02:00 (Lubumbashi) correctly
        // falls on May 8 even when the viewer is in Kinshasa (+01:00) or
        // the machine is in UTC.
        static DateOnly InvoiceDay(DateTimeOffset dto)
            => DateOnly.FromDateTime(dto.DateTime);

        // ── Last 7 days sales (LOCAL day buckets) ──
        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => todayLocal.AddDays(-6 + offset))
            .Select(day =>
            {
                var sales = invoices.Where(x => InvoiceDay(x.CreatedAt) == day && x.Type.IsSale())
                                      .Sum(x => x.TotalTTC);
                var credits = invoices.Where(x => InvoiceDay(x.CreatedAt) == day && x.Type.IsCreditNote())
                                      .Sum(x => x.TotalTTC);
                return new DailySalesPoint
                {
                    Date = day,
                    Amount = sales - credits,
                    Count = invoices.Count(x => InvoiceDay(x.CreatedAt) == day)
                };
            })
            .ToList();

        Debug.WriteLine($"  Last 7 local days: {string.Join(", ", last7Days.Select(d => $"{d.Date:dd/MM}={d.Amount:N0}"))}");

        // ── Month scope: first day of LOCAL current month ──
        var monthStartLocal = new DateOnly(todayLocal.Year, todayLocal.Month, 1);
        var monthInvoices = invoices
            .Where(i => InvoiceDay(i.CreatedAt) >= monthStartLocal)
            .ToList();
        Debug.WriteLine($"  Month invoices (since {monthStartLocal:yyyy-MM-dd} local): {monthInvoices.Count}");

        // ── Payment breakdown (this month) ──
        var paymentBreakdown = monthInvoices
            .Where(inv => inv.Type.IsSale())
            .SelectMany(inv => inv.Payments ?? [])
            .GroupBy(p => p.PaymentType)
            .Select(g => new PaymentBreakdownItem
            {
                PaymentType = g.Key,
                Total = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(p => p.Total)
            .ToList();

        Debug.WriteLine($"  Payment breakdown: {paymentBreakdown.Count} types → " +
            $"{string.Join(", ", paymentBreakdown.Select(p => $"{p.Label}={p.Total:N0}"))}");

        // ── Invoice type breakdown (this month) ──
        var typeBreakdown = monthInvoices
            .GroupBy(i => i.Type)
            .Select(g => new InvoiceTypeBreakdownItem
            {
                Type = g.Key,
                Count = g.Count(),
                Total = g.Sum(i => i.TotalTTC)
            })
            .OrderByDescending(i => i.Count)
            .ToList();

        Debug.WriteLine($"  Type breakdown: {string.Join(", ", typeBreakdown.Select(t => $"{t.Type}={t.Count}"))}");

        // ── Top 5 products (30 days) ──
        var topProducts = invoices
            .Where(inv => inv.Type.IsSale())
            .SelectMany(inv => inv.Lines ?? [])
            .GroupBy(l => string.IsNullOrEmpty(l.Name) ? l.Code : l.Name)
            .Select(g => new TopProductItem
            {
                Name = g.Key ?? "—",
                Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.AmountTTC)
            })
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToList();

        Debug.WriteLine($"  Top products: {topProducts.Count} → " +
            $"{string.Join(", ", topProducts.Select(p => $"{p.Name}={p.Revenue:N0}"))}");

        // ── Recent invoices ──
        var recentInvoices = invoices
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new RecentInvoiceItem
            {
                InvoiceNumber = i.InvoiceNumber,
                ClientName = string.IsNullOrWhiteSpace(i.ClientName)
                                ? "Client comptoir"
                                : i.ClientName,
                Amount = i.TotalTTC,
                Type = i.Type,
                Date = i.CreatedAt,            // DateTimeOffset — offset preserved
                Status = i.Status
            })
            .ToList();

        Debug.WriteLine($"  Recent invoices: {recentInvoices.Count}");

        // ── Period comparisons (LOCAL day) ──
        var yesterdayLocal = todayLocal.AddDays(-1);
        var yesterdaySales = invoices
            .Where(i => InvoiceDay(i.CreatedAt) == yesterdayLocal && i.Type.IsSale())
            .Sum(i => i.TotalTTC);

        // Week starts Monday (ISO 8601). DayOfWeek.Sunday = 0.
        var dow = (int)nowLocal.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var weekStartLocal = todayLocal.AddDays(-daysFromMonday);

        var weekSalesNet =
            invoices.Where(i => InvoiceDay(i.CreatedAt) >= weekStartLocal && i.Type.IsSale())
                    .Sum(i => i.TotalTTC)
          - invoices.Where(i => InvoiceDay(i.CreatedAt) >= weekStartLocal && i.Type.IsCreditNote())
                    .Sum(i => i.TotalTTC);

        var monthSalesNet =
            monthInvoices.Where(i => i.Type.IsSale()).Sum(i => i.TotalTTC)
          - monthInvoices.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalTTC);

        Debug.WriteLine($"\n── FINAL RESULTS ──");
        Debug.WriteLine($"  TodaySales={todayTotal:N0} TodayCount={todayCount}");
        Debug.WriteLine($"  WeekSales={weekSalesNet:N0} MonthSales={monthSalesNet:N0}");
        Debug.WriteLine($"  MonthInvoiceCount={monthInvoices.Count}");
        Debug.WriteLine($"  LowStock={lowStockAlerts}");
        Debug.WriteLine("╔══════════════════════════════════════════╗");
        Debug.WriteLine("║     DASHBOARD SERVICE - DONE             ║");
        Debug.WriteLine("╚══════════════════════════════════════════╝\n");

        return new DashboardData
        {
            TotalProducts = totalProducts,
            TotalClients = totalClients,
            LoyaltyMembers = loyaltyMembers,
            TodaySalesAmount = todayTotal,
            TodayInvoiceCount = todayCount,
            LowStockAlerts = lowStockAlerts,

            Last7DaysSales = last7Days,
            PaymentBreakdown = paymentBreakdown,
            InvoiceTypeBreakdown = typeBreakdown,
            TopProducts = topProducts,
            RecentInvoices = recentInvoices,

            YesterdaySalesAmount = yesterdaySales,
            WeekSalesAmount = weekSalesNet,
            MonthSalesAmount = monthSalesNet,
            MonthInvoiceCount = monthInvoices.Count
        };
    }
}

// ══════════════════════════════════════════════════════════
//  DTOs
// ══════════════════════════════════════════════════════════

public class DashboardData
{
    public decimal TodaySalesAmount { get; set; }
    public int TodayInvoiceCount { get; set; }
    public int TotalProducts { get; set; }
    public int TotalClients { get; set; }
    public int LoyaltyMembers { get; set; }
    public int LowStockAlerts { get; set; }

    public List<DailySalesPoint> Last7DaysSales { get; set; } = new();
    public List<PaymentBreakdownItem> PaymentBreakdown { get; set; } = new();
    public List<InvoiceTypeBreakdownItem> InvoiceTypeBreakdown { get; set; } = new();
    public List<TopProductItem> TopProducts { get; set; } = new();
    public List<RecentInvoiceItem> RecentInvoices { get; set; } = new();

    public decimal YesterdaySalesAmount { get; set; }
    public decimal WeekSalesAmount { get; set; }
    public decimal MonthSalesAmount { get; set; }
    public int MonthInvoiceCount { get; set; }
}

public class DailySalesPoint
{
    /// <summary>LOCAL calendar day (Kinshasa/Lubumbashi) represented by this bucket.</summary>
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class PaymentBreakdownItem
{
    public PaymentType PaymentType { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
    public string Label => PaymentType switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèque",
        PaymentType.Credit => "Crédit",
        _ => "Autre"
    };
}

public class InvoiceTypeBreakdownItem
{
    public InvoiceType Type { get; set; }
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class TopProductItem
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class RecentInvoiceItem
{
    public string InvoiceNumber { get; set; } = "";
    public string ClientName { get; set; } = "";
    public decimal Amount { get; set; }
    public InvoiceType Type { get; set; }

    /// <summary>Fiscal timestamp with offset preserved (anti-fraud audit).</summary>
    public DateTimeOffset Date { get; set; }
    public InvoiceStatus Status { get; set; }

    // ── View helpers (displayed at the invoice's own local wall clock) ──
    // Using .DateTime (wall clock at the invoice's stored offset) rather than
    // .LocalDateTime (viewer's machine offset) ensures a Lubumbashi invoice
    // still shows its Lubumbashi time even if the viewer is in Kinshasa.
    public string FormattedDate => Date.DateTime.ToString("dd/MM HH:mm");
    public string FormattedAmount => $"{Amount:N0} CDF";
    public string TypeLabel => Type.ToString();

    public string TypeColor => Type switch
    {
        InvoiceType.FV => "#37B1E4",
        InvoiceType.FT => "#8B5CF6",
        InvoiceType.FA => "#EF4444",
        InvoiceType.EV => "#10B981",
        InvoiceType.ET => "#F59E0B",
        InvoiceType.EA => "#EC4899",
        _ => "#94A3B8"
    };

    public string TypeBgColor => Type switch
    {
        InvoiceType.FV => "#E1F3FB",
        InvoiceType.FT => "#F3F0FF",
        InvoiceType.FA => "#FEF2F2",
        InvoiceType.EV => "#ECFDF5",
        InvoiceType.ET => "#FFFBEB",
        InvoiceType.EA => "#FDF2F8",
        _ => "#F1F5F9"
    };
}