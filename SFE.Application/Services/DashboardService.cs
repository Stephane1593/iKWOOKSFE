using SFE.Application.Interfaces;
using SFE.Domain.Enums;
using System.Diagnostics;

namespace SFE.Application.Services;

public class DashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardData> LoadDashboardAsync(int? activePosId = null)
    {
        Debug.WriteLine("╔══════════════════════════════════════════╗");
        Debug.WriteLine("║     DASHBOARD SERVICE - START            ║");
        Debug.WriteLine("╚══════════════════════════════════════════╝");
        Debug.WriteLine($"  activePosId = {activePosId?.ToString() ?? "NULL"}");
        Debug.WriteLine($"  DateTime.Now   = {DateTime.Now}");
        Debug.WriteLine($"  DateTime.Today = {DateTime.Today}");

        // ══════════ STEP 1: BASIC KPIs ══════════
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

        // ══════════ STEP 2: SEARCH INVOICES ══════════
        Debug.WriteLine("\n── STEP 2: Search invoices (last 30 days, Normalized) ──");

        var thirtyDaysAgo = DateTime.Today.AddDays(-30);
        Debug.WriteLine($"  DateFrom = {thirtyDaysAgo}");
        Debug.WriteLine($"  DateTo   = {DateTime.Now}");
        Debug.WriteLine($"  Status   = {InvoiceStatus.Normalized}");

        var result = await _unitOfWork.Invoices.SearchAsync(
            new InvoiceSearchCriteria
            {
                DateFrom = thirtyDaysAgo,
                DateTo = DateTime.Now,
                Status = InvoiceStatus.Normalized
            }, 1, int.MaxValue);

        Debug.WriteLine($"  ✅ SearchAsync returned: {result.Items.Count} invoices");

        // Log first 5 invoices
        foreach (var inv in result.Items.Take(5))
        {
            Debug.WriteLine(
                $"    → {inv.InvoiceNumber} | Created={inv.CreatedAt:yyyy-MM-dd HH:mm} | " +
                $"Type={inv.Type} | TTC={inv.TotalTTC:N0} | " +
                $"PosId={inv.PointOfSaleId} | " +
                $"Lines={inv.Lines?.Count ?? -1} | " +
                $"Payments={inv.Payments?.Count ?? -1}");
        }

        // ══════════ STEP 3: POS FILTER ══════════
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

        // ══════════ STEP 4: CHECK Lines & Payments ══════════
        Debug.WriteLine("\n── STEP 4: Lines & Payments check ──");
        var withLines = invoices.Count(i => i.Lines != null && i.Lines.Count > 0);
        var withPayments = invoices.Count(i => i.Payments != null && i.Payments.Count > 0);
        var nullLines = invoices.Count(i => i.Lines == null);
        var nullPayments = invoices.Count(i => i.Payments == null);
        Debug.WriteLine($"  Invoices with Lines:    {withLines} (null: {nullLines})");
        Debug.WriteLine($"  Invoices with Payments: {withPayments} (null: {nullPayments})");

        if (nullLines > 0)
            Debug.WriteLine($"  ⚠️ {nullLines} invoices have NULL Lines → " +
                            $"TopProducts will be EMPTY! Add .Include(i => i.Lines) in SearchAsync");
        if (nullPayments > 0)
            Debug.WriteLine($"  ⚠️ {nullPayments} invoices have NULL Payments → " +
                            $"PaymentBreakdown will be EMPTY! Add .Include(i => i.Payments) in SearchAsync");

        // ══════════ STEP 5: IsSale check ══════════
        Debug.WriteLine("\n── STEP 5: Type breakdown ──");
        var typeCounts = invoices.GroupBy(i => i.Type)
            .Select(g => $"{g.Key}={g.Count()} (IsSale={g.Key.IsSale()}, IsCreditNote={g.Key.IsCreditNote()})")
            .ToList();
        foreach (var tc in typeCounts)
            Debug.WriteLine($"    {tc}");

        var salesInvoices = invoices.Where(i => i.Type.IsSale()).ToList();
        Debug.WriteLine($"  Total sale invoices: {salesInvoices.Count}");

        // ══════════ BUILD DATA (original logic) ══════════
        Debug.WriteLine("\n── STEP 6: Building charts ──");

        // ── Last 7 days sales ──
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => DateTime.Today.AddDays(-6 + i))
            .Select(date =>
            {
                var sales = invoices.Where(x => x.CreatedAt.Date == date && x.Type.IsSale())
                    .Sum(x => x.TotalTTC);
                var credits = invoices.Where(x => x.CreatedAt.Date == date && x.Type.IsCreditNote())
                    .Sum(x => x.TotalTTC);
                return new DailySalesPoint
                {
                    Date = date,
                    Amount = sales - credits,
                    Count = invoices.Count(x => x.CreatedAt.Date == date)
                };
            })
            .ToList();

        Debug.WriteLine($"  Last 7 days: {string.Join(", ", last7Days.Select(d => $"{d.Date:dd/MM}={d.Amount:N0}"))}");

        // ── Payment breakdown (this month) ──
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthInvoices = invoices.Where(i => i.CreatedAt >= monthStart).ToList();
        Debug.WriteLine($"  Month invoices (since {monthStart:dd/MM}): {monthInvoices.Count}");

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
                    ? "Client comptoir" : i.ClientName,
                Amount = i.TotalTTC,
                Type = i.Type,
                Date = i.CreatedAt,
                Status = i.Status
            })
            .ToList();

        Debug.WriteLine($"  Recent invoices: {recentInvoices.Count}");

        // ── Period comparisons ──
        var yesterday = DateTime.Today.AddDays(-1);
        var yesterdaySales = invoices
            .Where(i => i.CreatedAt.Date == yesterday && i.Type.IsSale())
            .Sum(i => i.TotalTTC);

        var dow = (int)DateTime.Today.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var weekStart = DateTime.Today.AddDays(-daysFromMonday);

        var weekSalesNet = invoices.Where(i => i.CreatedAt.Date >= weekStart && i.Type.IsSale())
                            .Sum(i => i.TotalTTC)
                         - invoices.Where(i => i.CreatedAt.Date >= weekStart && i.Type.IsCreditNote())
                            .Sum(i => i.TotalTTC);

        var monthSalesNet = monthInvoices.Where(i => i.Type.IsSale()).Sum(i => i.TotalTTC)
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
    public DateTime Date { get; set; }
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
    public DateTime Date { get; set; }
    public InvoiceStatus Status { get; set; }

    // ── View helpers ──
    public string FormattedDate => Date.ToString("dd/MM HH:mm");
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