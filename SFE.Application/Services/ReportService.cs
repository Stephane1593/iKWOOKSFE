using System.Text;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class ReportService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ITimeProvider _time;   // DGI §1.1 — single source of truth

    public ReportService(IUnitOfWork uow, IAuditService audit, ITimeProvider time)
    {
        _uow = uow;
        _audit = audit;
        _time = time;
    }

    // ── Storage & comparisons: always UTC (DGI §1.1) ─────────────
    private DateTime UtcNow => _time.UtcNow.UtcDateTime;        // Kind=Utc
    private DateTime UtcToday =>
        _time.UtcToday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // ── Display only ─────────────────────────────────────────────
    private DateTimeOffset LocalDisplay => _time.LocalNow;

    /// <summary>Converts a stored UTC DateTime to local wall-clock for printing.</summary>
    private DateTime ToLocal(DateTime utc)
    {
        // DRC has no DST — offset is stable per site (UTC+1 Kinshasa, UTC+2 Lubumbashi/Goma).
        var kind = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        return kind.Add(LocalDisplay.Offset);
    }

    // ══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════

    public async Task<DailyReport> GenerateReportXAsync(string operatorName)
    {
        var lastZ = await GetLastReportDateAsync(ReportType.Z);
        var periodStart = lastZ ?? UtcToday;
        var periodEnd = UtcNow;

        var report = await BuildZXReportAsync(ReportType.X, periodStart, periodEnd, operatorName);
        report.IsPeriodic = false;
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.X);
        report.PrintContent = FormatZXReport(report);

        await SaveReportAsync(report);
        await _audit.LogReportAsync(AuditAction.ReportXGenerated, "X",
                report.Id, $"Quotidien · {report.TotalInvoiceCount} factures · " +
                $"TTC net: {report.GrandTotalTTC:N2} CDF");
        return report;
    }

    public async Task<DailyReport> GenerateReportXPeriodicAsync(
        string operatorName, DateTime periodStart, DateTime periodEnd)
    {
        var report = await BuildZXReportAsync(ReportType.X, periodStart, periodEnd, operatorName);
        report.IsPeriodic = true;
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.X);
        report.PrintContent = FormatZXReport(report);

        await SaveReportAsync(report);
        await _audit.LogReportAsync(AuditAction.ReportXGenerated, "X-Périodique",
                report.Id, $"Périodique du {periodStart:dd/MM/yyyy} au {periodEnd:dd/MM/yyyy} · " +
                $"{report.TotalInvoiceCount} factures · TTC net: {report.GrandTotalTTC:N2} CDF");
        return report;
    }

    public async Task<DailyReport> GenerateReportZAsync(string operatorName)
    {
        var lastZ = await GetLastReportDateAsync(ReportType.Z);
        var periodStart = lastZ ?? UtcToday;
        var periodEnd = UtcNow;

        var report = await BuildZXReportAsync(ReportType.Z, periodStart, periodEnd, operatorName);
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.Z);
        report.PrintContent = FormatZXReport(report);

        await SaveReportAsync(report);
        await _audit.LogReportAsync(AuditAction.ReportZGenerated, "Z",
                report.Id, $"{report.TotalInvoiceCount} factures · " +
                $"TTC net: {report.GrandTotalTTC:N2} CDF");
        return report;
    }

    public async Task<DailyReport> GenerateReportAAsync(string operatorName)
    {
        var lastA = await GetLastReportDateAsync(ReportType.A);
        var periodStart = lastA ?? UtcToday;
        var periodEnd = UtcNow;

        var report = await BuildAReportAsync(periodStart, periodEnd, operatorName);
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.A);
        report.PrintContent = FormatAReport(report);

        await SaveReportAsync(report);
        await _audit.LogReportAsync(AuditAction.ReportAGenerated, "A",
                report.Id, $"{report.ArticleLines.Count} articles · " +
                $"Montant net: {report.ArticleLines.Sum(a => a.TotalAmount):N2} CDF");
        return report;
    }

    // ══════════════════════════════════════════════════════════
    //  Session Z-Report
    // ══════════════════════════════════════════════════════════

    public async Task<DailyReport> GenerateSessionZReportAsync(SessionCloseData closeData)
    {
        // DGI §1.1 — convert the session boundary to UTC for storage & querying.
        var periodStart = closeData.SessionOpenedAt.UtcDateTime;  // Kind=Utc
        var periodEnd = UtcNow;

        var report = await BuildZXReportAsync(
            ReportType.Z, periodStart, periodEnd,
            closeData.OperatorName, closeData.PointOfSaleId);

        report.ReportNumber = await GetNextReportNumberAsync(ReportType.Z);
        report.PointOfSaleId = closeData.PointOfSaleId;

        // Opening
        report.SessionOpenedAt = periodStart;  // store the same UTC value we queried with
        report.OpeningAmountUSD = closeData.OpeningAmountUSD;
        report.OpeningAmountCDF = closeData.OpeningAmountCDF;
        report.OpeningAmountEUR = closeData.OpeningAmountEUR;
        report.OpeningAmountCNY = closeData.OpeningAmountCNY;
        report.RateUSD = closeData.RateUSD;
        report.RateEUR = closeData.RateEUR;
        report.RateCNY = closeData.RateCNY;
        report.OpeningNotes = closeData.OpeningNotes;

        // Expected
        var invoices = await FetchInvoicesAsync(
            periodStart, periodEnd, InvoiceStatus.Normalized, closeData.PointOfSaleId);

        var expected = CalculateExpectedCash(invoices, closeData);
        report.ExpectedCashUSD = expected.usd;
        report.ExpectedCashCDF = expected.cdf;
        report.ExpectedCashEUR = expected.eur;
        report.ExpectedCashCNY = expected.cny;

        // Closing
        report.ClosingAmountUSD = closeData.ClosingAmountUSD;
        report.ClosingAmountCDF = closeData.ClosingAmountCDF;
        report.ClosingAmountEUR = closeData.ClosingAmountEUR;
        report.ClosingAmountCNY = closeData.ClosingAmountCNY;
        report.ClosingNotes = closeData.ClosingNotes;

        // Variance
        report.VarianceUSD = closeData.ClosingAmountUSD - expected.usd;
        report.VarianceCDF = closeData.ClosingAmountCDF - expected.cdf;
        report.VarianceEUR = closeData.ClosingAmountEUR - expected.eur;
        report.VarianceCNY = closeData.ClosingAmountCNY - expected.cny;

        report.PrintContent = FormatZXReport(report);
        await SaveReportAsync(report);

        // Audit line: render the opening time in the operator's wall-clock (local)
        // so the log reads the way the operator saw it in the UI.
        await _audit.LogReportAsync(AuditAction.ReportZGenerated, "Z-Session",
                report.Id,
                $"Session du {closeData.SessionOpenedAt.LocalDateTime:dd/MM/yyyy HH:mm} · " +
                $"PDV #{closeData.PointOfSaleId} · {report.TotalInvoiceCount} factures · " +
                $"TTC net: {report.GrandTotalTTC:N2} CDF · " +
                $"Écart caisse: {report.VarianceTotalCDF:N0} CDF");

        return report;
    }

    public async Task<SessionSummary> CalculateSessionSummaryAsync(
        DateTimeOffset sessionStart, int pointOfSaleId,
        decimal openUSD, decimal openCDF, decimal openEUR, decimal openCNY)
    {
        // DGI §1.1 — convert the session boundary to UTC for storage & querying.
        var startUtc = sessionStart.UtcDateTime;   // Kind=Utc
        var now = UtcNow;

        var invoices = await FetchInvoicesAsync(
            startUtc, now, InvoiceStatus.Normalized, pointOfSaleId);

        int salesCount = invoices.Count(i => i.Type.IsSale());
        int creditCount = invoices.Count(i => i.Type.IsCreditNote());

        decimal salesTTC = invoices.Where(i => i.Type.IsSale()).Sum(i => i.TotalTTC);
        decimal creditTTC = invoices.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalTTC);

        int incomplete = 0;
        foreach (var status in new[] { InvoiceStatus.Draft, InvoiceStatus.Error, InvoiceStatus.Cancelled })
        {
            var res = await _uow.Invoices.SearchAsync(
                new InvoiceSearchCriteria { DateFrom = startUtc, DateTo = now, Status = status },
                1, int.MaxValue);
            incomplete += res.Items.Count(i => i.PointOfSaleId == pointOfSaleId);
        }

        var cashByCurrency = CalculateCashFlowByCurrency(invoices);

        decimal expUSD = openUSD + cashByCurrency.salesUSD - cashByCurrency.refundsUSD;
        decimal expCDF = openCDF + cashByCurrency.salesCDF - cashByCurrency.refundsCDF;
        decimal expEUR = openEUR + cashByCurrency.salesEUR - cashByCurrency.refundsEUR;
        decimal expCNY = openCNY + cashByCurrency.salesCNY - cashByCurrency.refundsCNY;

        decimal nonCashTotal = invoices
            .SelectMany(i => i.Payments)
            .Where(p => p.PaymentType != PaymentType.Especes)
            .Sum(p => p.Amount);

        return new SessionSummary
        {
            TotalInvoiceCount = invoices.Count,
            SalesCount = salesCount,
            CreditNoteCount = creditCount,
            SalesTTC = salesTTC,
            CreditNoteTTC = creditTTC,
            NetTTC = salesTTC - creditTTC,
            IncompleteCount = incomplete,

            CashSalesUSD = cashByCurrency.salesUSD,
            CashSalesCDF = cashByCurrency.salesCDF,
            CashSalesEUR = cashByCurrency.salesEUR,
            CashSalesCNY = cashByCurrency.salesCNY,

            CashRefundsUSD = cashByCurrency.refundsUSD,
            CashRefundsCDF = cashByCurrency.refundsCDF,
            CashRefundsEUR = cashByCurrency.refundsEUR,
            CashRefundsCNY = cashByCurrency.refundsCNY,

            ExpectedCashUSD = expUSD,
            ExpectedCashCDF = expCDF,
            ExpectedCashEUR = expEUR,
            ExpectedCashCNY = expCNY,

            NonCashTotal = nonCashTotal
        };
    }

    // ══════════════════════════════════════════════════════════
    //  BUILD Z/X REPORT (§1.3)
    // ══════════════════════════════════════════════════════════

    private async Task<DailyReport> BuildZXReportAsync(
        ReportType type, DateTime start, DateTime end, string operatorName,
        int? posId = null)
    {
        var company = await GetCompanyAsync();

        var normalized = await FetchInvoicesAsync(start, end, InvoiceStatus.Normalized, posId);

        // §1.3.3.m — Incomplete
        int incompleteCount = 0;
        foreach (var status in new[] { InvoiceStatus.Draft, InvoiceStatus.Error, InvoiceStatus.Cancelled })
        {
            var incomplete = await _uow.Invoices.SearchAsync(
                new InvoiceSearchCriteria { DateFrom = start, DateTo = end, Status = status },
                1, int.MaxValue);
            var items = incomplete.Items;
            if (posId.HasValue)
                items = items.Where(i => i.PointOfSaleId == posId.Value).ToList();
            incompleteCount += items.Count;
        }

        var report = new DailyReport
        {
            Type = type,
            PeriodStart = start,
            PeriodEnd = end,
            GeneratedAt = UtcNow,   // ← ITimeProvider
            OperatorName = operatorName,
            CompanyName = company?.Name ?? "",
            CompanyNIF = company?.NIF ?? "",
            ISF = company?.ISF ?? "",
            TotalInvoiceCount = normalized.Count,
            TotalItemCount = normalized.Sum(i => i.Lines.Count),
            IncompleteCount = incompleteCount,
        };

        if (posId.HasValue)
            report.PointOfSaleId = posId.Value;

        // §1.3.3.g — Totaux par type
        foreach (InvoiceType invType in Enum.GetValues<InvoiceType>())
        {
            var subset = normalized.Where(i => i.Type == invType).ToList();
            if (subset.Count == 0) continue;

            report.InvoiceTypeSummaries.Add(new ReportInvoiceTypeSummary
            {
                InvoiceType = invType,
                Count = subset.Count,
                TotalHT = subset.Sum(i => i.TotalHT),
                TotalTVA = subset.Sum(i => i.TotalTVA),
                TotalTTC = subset.Sum(i => i.TotalTTC),
                TotalSpecificTax = subset.Sum(i => i.TotalSpecificTax)
            });

            var allLines = subset.SelectMany(i => i.Lines).ToList();

            foreach (TaxGroup tg in Enum.GetValues<TaxGroup>())
            {
                var groupLines = allLines.Where(l => l.TaxGroup == tg).ToList();
                if (groupLines.Count == 0) continue;

                report.TaxGroupDetails.Add(new ReportTaxGroupDetail
                {
                    InvoiceType = invType,
                    TaxGroup = tg,
                    TotalAmount = groupLines.Sum(l => l.AmountTTC),
                    TaxableAmount = groupLines.Sum(l => l.AmountHT),
                    TaxAmount = groupLines.Sum(l => l.AmountTVA)
                });
            }
        }

        // §1.3.3.j,k — Payments
        foreach (PaymentType pt in Enum.GetValues<PaymentType>())
        {
            var invoicesWithPt = normalized
                .Where(i => i.Payments.Any(p => p.PaymentType == pt))
                .ToList();
            if (invoicesWithPt.Count == 0) continue;

            decimal totalAmount = normalized
                .SelectMany(i => i.Payments)
                .Where(p => p.PaymentType == pt)
                .Sum(p => p.Amount);

            report.PaymentSummaries.Add(new ReportPaymentSummary
            {
                PaymentType = pt,
                InvoiceCount = invoicesWithPt.Count,
                TotalAmount = totalAmount
            });
        }

        // Net totals
        decimal salesHT = normalized.Where(i => i.Type.IsSale()).Sum(i => i.TotalHT);
        decimal salesTVA = normalized.Where(i => i.Type.IsSale()).Sum(i => i.TotalTVA);
        decimal salesTTC = normalized.Where(i => i.Type.IsSale()).Sum(i => i.TotalTTC);
        decimal cnHT = normalized.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalHT);
        decimal cnTVA = normalized.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalTVA);
        decimal cnTTC = normalized.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalTTC);

        report.GrandTotalHT = salesHT - cnHT;
        report.GrandTotalTVA = salesTVA - cnTVA;
        report.GrandTotalTTC = salesTTC - cnTTC;
        report.TotalSpecificTax = normalized.Where(i => i.Type.IsSale()).Sum(i => i.TotalSpecificTax)
                                  - normalized.Where(i => i.Type.IsCreditNote()).Sum(i => i.TotalSpecificTax);

        return report;
    }

    // ══════════════════════════════════════════════════════════
    //  BUILD A-REPORT (§1.4)
    // ══════════════════════════════════════════════════════════

    private async Task<DailyReport> BuildAReportAsync(
        DateTime start, DateTime end, string operatorName)
    {
        var company = await GetCompanyAsync();
        var normalized = await FetchInvoicesAsync(start, end, InvoiceStatus.Normalized);

        var salesLines = normalized.Where(i => i.Type.IsSale()).SelectMany(i => i.Lines).ToList();
        var creditLines = normalized.Where(i => i.Type.IsCreditNote()).SelectMany(i => i.Lines).ToList();

        var allCodes = salesLines.Select(l => l.Code)
            .Union(creditLines.Select(l => l.Code))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var allProducts = (await _uow.GetRepository<Product>().FindAsync(p => true)).ToList();
        var productByCode = allProducts
            .Where(p => !string.IsNullOrEmpty(p.Code))
            .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allPosStocks = (await _uow.GetRepository<PosStock>().FindAsync(ps => true)).ToList();
        var totalStockByProductId = allPosStocks
            .GroupBy(ps => ps.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(ps => ps.Quantity));

        var report = new DailyReport
        {
            Type = ReportType.A,
            PeriodStart = start,
            PeriodEnd = end,
            GeneratedAt = UtcNow,   // ← ITimeProvider
            OperatorName = operatorName,
            CompanyName = company?.Name ?? "",
            CompanyNIF = company?.NIF ?? "",
            ISF = company?.ISF ?? "",
        };

        foreach (var code in allCodes)
        {
            var sold = salesLines.Where(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).ToList();
            var returned = creditLines.Where(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).ToList();
            var sample = sold.FirstOrDefault() ?? returned.First();

            decimal stockQty = 0;
            if (productByCode.TryGetValue(code, out var product))
            {
                if (totalStockByProductId.TryGetValue(product.Id, out var posQty) && posQty > 0)
                    stockQty = posQty;
                else
                    stockQty = product.StockQuantity;
            }

            decimal unitPrice = product?.UnitPrice ?? sample.UnitPriceHT;

            report.ArticleLines.Add(new ArticleReportLine
            {
                ArticleCode = code,
                ArticleName = sample.Name,
                UnitPrice = unitPrice,
                TaxRate = sample.TaxRate,
                TaxGroup = sample.TaxGroup,
                QuantitySold = sold.Sum(l => l.Quantity),
                QuantityReturned = returned.Sum(l => l.Quantity),
                QuantityInStock = stockQty,
                TotalAmount = sold.Sum(l => l.AmountTTC) - returned.Sum(l => l.AmountTTC)
            });
        }

        report.TotalInvoiceCount = normalized.Count;
        report.TotalItemCount = report.ArticleLines.Count;
        return report;
    }

    // ══════════════════════════════════════════════════════════
    //  EXPECTED CASH CALCULATION
    // ══════════════════════════════════════════════════════════

    private (decimal usd, decimal cdf, decimal eur, decimal cny) CalculateExpectedCash(
        List<Invoice> invoices, SessionCloseData closeData)
    {
        decimal cashUSD = closeData.OpeningAmountUSD;
        decimal cashCDF = closeData.OpeningAmountCDF;
        decimal cashEUR = closeData.OpeningAmountEUR;
        decimal cashCNY = closeData.OpeningAmountCNY;

        foreach (var inv in invoices)
        {
            var cashTotal = inv.Payments
                .Where(p => p.PaymentType == PaymentType.Especes)
                .Sum(p => p.Amount);

            if (cashTotal <= 0) continue;

            var netCash = Math.Min(cashTotal, inv.TotalTTC);

            var currency = NormalizeCurrency(inv.CurrencyCode);
            int sign = inv.Type.IsSale() ? 1 : inv.Type.IsCreditNote() ? -1 : 0;
            if (sign == 0) continue;

            switch (currency)
            {
                case "USD": cashUSD += netCash * sign; break;
                case "EUR": cashEUR += netCash * sign; break;
                case "CNY": cashCNY += netCash * sign; break;
                default: cashCDF += netCash * sign; break;
            }
        }

        return (cashUSD, cashCDF, cashEUR, cashCNY);
    }

    private CashFlowByCurrency CalculateCashFlowByCurrency(List<Invoice> invoices)
    {
        var result = new CashFlowByCurrency();

        foreach (var inv in invoices)
        {
            var cashTotal = inv.Payments
                .Where(p => p.PaymentType == PaymentType.Especes)
                .Sum(p => p.Amount);

            if (cashTotal <= 0) continue;
            var netCash = Math.Min(cashTotal, inv.TotalTTC);
            var currency = NormalizeCurrency(inv.CurrencyCode);

            if (inv.Type.IsSale())
            {
                switch (currency)
                {
                    case "USD": result.salesUSD += netCash; break;
                    case "EUR": result.salesEUR += netCash; break;
                    case "CNY": result.salesCNY += netCash; break;
                    default: result.salesCDF += netCash; break;
                }
            }
            else if (inv.Type.IsCreditNote())
            {
                switch (currency)
                {
                    case "USD": result.refundsUSD += netCash; break;
                    case "EUR": result.refundsEUR += netCash; break;
                    case "CNY": result.refundsCNY += netCash; break;
                    default: result.refundsCDF += netCash; break;
                }
            }
        }

        return result;
    }

    private static string NormalizeCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "CDF";
        return code.Trim().ToUpperInvariant() switch
        {
            "USD" => "USD",
            "EUR" => "EUR",
            "CNY" => "CNY",
            _ => "CDF"
        };
    }

    // ══════════════════════════════════════════════════════════
    //  FORMAT Z/X REPORT → TEXT
    // ══════════════════════════════════════════════════════════

    private string FormatZXReport(DailyReport r)
    {
        const int W = 56;
        var heavy = new string('═', W);
        var thin = new string('─', W);
        var sb = new StringBuilder();

        sb.AppendLine(heavy);
        sb.AppendLine(Center(r.CompanyName, W));
        sb.AppendLine(Center($"NIF: {r.CompanyNIF}", W));
        sb.AppendLine(heavy);
        sb.AppendLine();

        string reportTitle = r.Type == ReportType.Z
            ? "Z-RAPPORT (Clôture)"
            : r.IsPeriodic ? "X-RAPPORT PÉRIODIQUE" : "X-RAPPORT QUOTIDIEN";
        sb.AppendLine(Center(reportTitle, W));
        sb.AppendLine(Center($"N° {r.ReportNumber}", W));
        sb.AppendLine();

        sb.AppendLine($"  Date       : {r.GeneratedAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"  ISF        : {r.ISF}");
        sb.AppendLine($"  Période du : {r.PeriodStart:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"          au : {r.PeriodEnd:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"  Opérateur  : {r.OperatorName}");
        sb.AppendLine();

        // ── SESSION DE CAISSE (Z-report only) ──
        if (r.HasSessionData)
        {
            sb.AppendLine(thin);
            sb.AppendLine(Center("SESSION DE CAISSE", W));
            sb.AppendLine(thin);
            sb.AppendLine($"  Ouverture  : {r.SessionOpenedAt:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"  Clôture    : {r.GeneratedAt:dd/MM/yyyy HH:mm}");

            var duration = r.GeneratedAt - r.SessionOpenedAt!.Value;
            sb.AppendLine($"  Durée      : {(int)duration.TotalHours}h {duration.Minutes:D2}min");
            sb.AppendLine();

            sb.AppendLine($"  {"Devise",-8} {"Ouverture",12} {"Attendu",12} {"Compté",12} {"Écart",12}");
            sb.AppendLine($"  {new string('─', W - 4)}");

            AppendCurrencyRow(sb, "USD", r.OpeningAmountUSD, r.ExpectedCashUSD, r.ClosingAmountUSD, r.VarianceUSD);
            AppendCurrencyRow(sb, "CDF", r.OpeningAmountCDF, r.ExpectedCashCDF, r.ClosingAmountCDF, r.VarianceCDF);
            AppendCurrencyRow(sb, "EUR", r.OpeningAmountEUR, r.ExpectedCashEUR, r.ClosingAmountEUR, r.VarianceEUR);
            AppendCurrencyRow(sb, "CNY", r.OpeningAmountCNY, r.ExpectedCashCNY, r.ClosingAmountCNY, r.VarianceCNY);

            sb.AppendLine($"  {new string('─', W - 4)}");
            sb.AppendLine($"  {"Éq.CDF",-8} {r.OpeningTotalCDF,12:N0} {r.ExpectedTotalCDF,12:N0} {r.ClosingTotalCDF,12:N0} {r.VarianceTotalCDF,12:N0}");

            if (r.VarianceTotalCDF == 0)
                sb.AppendLine(Center("✓ Caisse équilibrée", W));
            else if (r.VarianceTotalCDF > 0)
                sb.AppendLine(Center($"⚠ Excédent de {r.VarianceTotalCDF:N0} CDF", W));
            else
                sb.AppendLine(Center($"⚠ Manquant de {Math.Abs(r.VarianceTotalCDF):N0} CDF", W));

            sb.AppendLine();

            if (r.RateUSD > 0) sb.AppendLine($"  Taux USD   : {r.RateUSD:N2} CDF");
            if (r.RateEUR > 0) sb.AppendLine($"  Taux EUR   : {r.RateEUR:N2} CDF");
            if (r.RateCNY > 0) sb.AppendLine($"  Taux CNY   : {r.RateCNY:N2} CDF");
            sb.AppendLine();
        }

        // ── Invoice type summaries ──
        sb.AppendLine(thin);
        sb.AppendLine(Center("TOTAUX PAR TYPE DE FACTURE", W));
        sb.AppendLine(thin);
        sb.AppendLine($"  {"Type",-10} {"Nb",4} {"HT",12} {"TVA",12} {"TTC",12}");
        sb.AppendLine($"  {new string('─', W - 4)}");

        foreach (var ts in r.InvoiceTypeSummaries.OrderBy(s => s.InvoiceType))
        {
            sb.AppendLine(
                $"  {ts.InvoiceType,-10} {ts.Count,4}" +
                $" {ts.TotalHT,12:N2} {ts.TotalTVA,12:N2} {ts.TotalTTC,12:N2}");
            if (ts.TotalSpecificTax > 0)
                sb.AppendLine($"  {"  T.Spécif.",-14} {"",-4} {"",-12} {"",-12} {ts.TotalSpecificTax,12:N2}");
        }
        sb.AppendLine();

        // ── Tax group details ──
        sb.AppendLine(thin);
        sb.AppendLine(Center("DÉTAIL PAR GROUPE DE TAXATION", W));
        sb.AppendLine(thin);

        var invoiceTypesInReport = r.TaxGroupDetails
            .Select(d => d.InvoiceType).Distinct().OrderBy(t => t);

        foreach (var invType in invoiceTypesInReport)
        {
            sb.AppendLine();
            sb.AppendLine($"  ┌─ {invType} — {invType.Label()}");
            sb.AppendLine($"  │ {"Grp",-6} {"Total",12} {"Taxable",12} {"TVA",12}");
            sb.AppendLine($"  │ {new string('─', W - 6)}");

            var details = r.TaxGroupDetails.Where(d => d.InvoiceType == invType).OrderBy(d => d.TaxGroup);
            decimal subTotal = 0, subTaxable = 0, subTax = 0;

            foreach (var d in details)
            {
                char label = (char)('A' + (int)d.TaxGroup);
                string desc = GetTaxGroupShortDesc(d.TaxGroup);
                sb.AppendLine(
                    $"  │ {label} {desc,-4} {d.TotalAmount,12:N2} {d.TaxableAmount,12:N2} {d.TaxAmount,12:N2}");
                subTotal += d.TotalAmount;
                subTaxable += d.TaxableAmount;
                subTax += d.TaxAmount;
            }

            sb.AppendLine($"  │ {new string('─', W - 6)}");
            sb.AppendLine($"  │ {"TOTAL",-6} {subTotal,12:N2} {subTaxable,12:N2} {subTax,12:N2}");
            sb.AppendLine($"  └─");
        }
        sb.AppendLine();

        // ── Payment breakdown ──
        sb.AppendLine(thin);
        sb.AppendLine(Center("VENTILATION DES PAIEMENTS", W));
        sb.AppendLine(thin);
        sb.AppendLine($"  {"Mode de paiement",-26} {"Nb fact.",8} {"Montant",14}");
        sb.AppendLine($"  {new string('─', W - 4)}");

        int totalPaymentInvoices = 0;
        decimal totalPaymentAmount = 0;

        foreach (var ps in r.PaymentSummaries.OrderBy(p => p.PaymentType))
        {
            sb.AppendLine($"  {GetPaymentLabel(ps.PaymentType),-26} {ps.InvoiceCount,8} {ps.TotalAmount,14:N2}");
            totalPaymentInvoices += ps.InvoiceCount;
            totalPaymentAmount += ps.TotalAmount;
        }

        sb.AppendLine($"  {new string('─', W - 4)}");
        sb.AppendLine($"  {"TOTAL",-26} {totalPaymentInvoices,8} {totalPaymentAmount,14:N2}");
        sb.AppendLine();

        // ── Credit notes ──
        var creditSummary = r.InvoiceTypeSummaries.Where(s => s.InvoiceType.IsCreditNote()).ToList();
        if (creditSummary.Any())
        {
            sb.AppendLine(thin);
            sb.AppendLine(Center("ÉLÉMENTS RÉDUCTEURS", W));
            sb.AppendLine(thin);
            foreach (var cn in creditSummary)
                sb.AppendLine($"  {cn.InvoiceType.Label(),-30} {cn.Count,4} × {cn.TotalTTC,14:N2}");
            sb.AppendLine();
        }

        // ── Grand totals ──
        sb.AppendLine(heavy);
        sb.AppendLine($"  {"Total HT net",-34} {r.GrandTotalHT,14:N2} CDF");
        sb.AppendLine($"  {"Total TVA",-34} {r.GrandTotalTVA,14:N2} CDF");
        if (r.TotalSpecificTax != 0)
            sb.AppendLine($"  {"Taxes spécifiques",-34} {r.TotalSpecificTax,14:N2} CDF");
        sb.AppendLine(heavy);
        sb.AppendLine($"  {"TOTAL TTC NET",-34} {r.GrandTotalTTC,14:N2} CDF");
        sb.AppendLine(heavy);
        sb.AppendLine();

        sb.AppendLine($"  {"Factures normalisées",-34} {r.TotalInvoiceCount,10}");
        sb.AppendLine($"  {"Total articles facturés",-34} {r.TotalItemCount,10}");
        sb.AppendLine($"  {"Ventes incomplètes",-34} {r.IncompleteCount,10}");
        sb.AppendLine();

        // ── Footer ──
        sb.AppendLine(heavy);
        if (r.Type == ReportType.Z)
        {
            sb.AppendLine(Center("*** CLÔTURE DE SESSION ***", W));
            sb.AppendLine(Center("Compteurs remis à zéro", W));
        }
        else
        {
            sb.AppendLine(Center(r.IsPeriodic
                ? "--- Rapport périodique (lecture seule) ---"
                : "--- Rapport quotidien (lecture seule) ---", W));
        }
        sb.AppendLine(heavy);
        sb.AppendLine(Center($"Imprimé le {UtcNow:dd/MM/yyyy HH:mm}", W));   // ← ITimeProvider
        sb.AppendLine(Center($"ISF: {r.ISF}", W));

        return sb.ToString();
    }

    private static void AppendCurrencyRow(StringBuilder sb, string label,
        decimal opening, decimal expected, decimal closing, decimal variance)
    {
        string vSign = variance switch
        {
            > 0 => "+",
            < 0 => "",
            _ => " "
        };
        sb.AppendLine($"  {label,-8} {opening,12:N2} {expected,12:N2} {closing,12:N2} {vSign}{variance,11:N2}");
    }

    // ══════════════════════════════════════════════════════════
    //  FORMAT A-REPORT → TEXT (§1.4)
    // ══════════════════════════════════════════════════════════

    private string FormatAReport(DailyReport r)
    {
        const int W = 76;
        var heavy = new string('═', W);
        var thin = new string('─', W);
        var sb = new StringBuilder();

        sb.AppendLine(heavy);
        sb.AppendLine(Center(r.CompanyName, W));
        sb.AppendLine(Center($"NIF: {r.CompanyNIF}", W));
        sb.AppendLine(heavy);
        sb.AppendLine();
        sb.AppendLine(Center("A-RAPPORT", W));
        sb.AppendLine(Center($"N° {r.ReportNumber}", W));
        sb.AppendLine();

        sb.AppendLine($"  Date       : {r.GeneratedAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"  ISF        : {r.ISF}");
        sb.AppendLine($"  Période du : {r.PeriodStart:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"          au : {r.PeriodEnd:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"  Opérateur  : {r.OperatorName}");
        sb.AppendLine();

        sb.AppendLine(thin);
        sb.AppendLine(
            $"  {"Code",-12} {"Désignation",-20} {"P.U.",10} {"Taux",6} {"Vendu",8} {"Retour",8} {"Stock",8}");
        sb.AppendLine(thin);

        foreach (var a in r.ArticleLines)
        {
            string name = a.ArticleName.Length > 20 ? a.ArticleName[..17] + "..." : a.ArticleName;
            char grpChar = (char)('A' + (int)a.TaxGroup);
            string taxLabel = $"{grpChar}/{a.TaxRate:G}%";
            sb.AppendLine(
                $"  {a.ArticleCode,-12} {name,-20} {a.UnitPrice,10:N2} {taxLabel,6}" +
                $" {a.QuantitySold,8:N3} {a.QuantityReturned,8:N3} {a.QuantityInStock,8:N3}");
        }

        sb.AppendLine(thin);
        sb.AppendLine();
        sb.AppendLine($"  {"Total articles distincts :",-40} {r.ArticleLines.Count,10}");
        sb.AppendLine($"  {"Total quantité vendue :",-40} {r.ArticleLines.Sum(a => a.QuantitySold),10:N3}");
        sb.AppendLine($"  {"Total quantité retournée :",-40} {r.ArticleLines.Sum(a => a.QuantityReturned),10:N3}");
        sb.AppendLine($"  {"Montant net (ventes − retours) :",-40} {r.ArticleLines.Sum(a => a.TotalAmount),10:N2} CDF");
        sb.AppendLine();
        sb.AppendLine(heavy);
        sb.AppendLine(Center($"Imprimé le {UtcNow:dd/MM/yyyy HH:mm}", W));   // ← ITimeProvider
        sb.AppendLine(Center($"ISF: {r.ISF}", W));

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private async Task<List<Invoice>> FetchInvoicesAsync(
        DateTime start, DateTime end, InvoiceStatus status, int? posId = null)
    {
        var result = await _uow.Invoices.SearchAsync(
            new InvoiceSearchCriteria
            {
                DateFrom = start,
                DateTo = end,
                Status = status
            }, 1, int.MaxValue);

        var items = result.Items;
        if (posId.HasValue)
            items = items.Where(i => i.PointOfSaleId == posId.Value).ToList();

        return items;
    }

    private async Task<Company?> GetCompanyAsync() =>
        await _uow.Companies.GetCurrentCompanyAsync();

    private async Task SaveReportAsync(DailyReport report)
    {
        await _uow.GetRepository<DailyReport>().AddAsync(report);
        await _uow.SaveChangesAsync();
    }

    /// <summary>
    /// Returns the last report's PeriodEnd as a local wall-clock DateTime.
    /// Works whether PeriodEnd is DateTime (Unspecified/Local) or DateTimeOffset.
    /// </summary>
    private async Task<DateTime?> GetLastReportDateAsync(ReportType type)
    {
        var reports = await _uow.GetRepository<DailyReport>().FindAsync(r => r.Type == type);
        var last = reports.OrderByDescending(r => r.GeneratedAt).FirstOrDefault();
        if (last is null) return null;

        // Safe across entity migrations: PeriodEnd may be DateTime or DateTimeOffset.
        // Using dynamic keeps this resilient; cost is negligible (called once per report).
        dynamic pe = last.PeriodEnd;
        try { return (DateTime)pe.LocalDateTime; }       // DateTimeOffset path
        catch { return (DateTime)pe; }                   // DateTime path
    }

    private async Task<int> GetNextReportNumberAsync(ReportType type)
    {
        var reports = await _uow.GetRepository<DailyReport>().FindAsync(r => r.Type == type);
        return reports.Count() + 1;
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        int pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string GetPaymentLabel(PaymentType type) => type switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèque",
        PaymentType.Credit => "Crédit",
        PaymentType.Autre => "Autre",
        _ => type.ToString()
    };

    private static string GetTaxGroupShortDesc(TaxGroup tg) => tg switch
    {
        TaxGroup.A => "Exo",
        TaxGroup.B => "16%",
        TaxGroup.C => "5%",
        TaxGroup.D => "Dér",
        TaxGroup.E => "Exp",
        TaxGroup.F => "MP16",
        TaxGroup.G => "MP5",
        TaxGroup.H => "Con",
        TaxGroup.I => "Gar",
        TaxGroup.J => "Déb",
        TaxGroup.K => "NA",
        TaxGroup.L => "Pré",
        TaxGroup.M => "Rég",
        TaxGroup.N => "TSp",
        TaxGroup.O => "1%",
        TaxGroup.P => "MP1",
        _ => ""
    };
}

// ══════════════════════════════════════════════════════════
//  HELPER DTOs
// ══════════════════════════════════════════════════════════

public class SessionSummary
{
    public int TotalInvoiceCount { get; set; }
    public int SalesCount { get; set; }
    public int CreditNoteCount { get; set; }
    public decimal SalesTTC { get; set; }
    public decimal CreditNoteTTC { get; set; }
    public decimal NetTTC { get; set; }
    public int IncompleteCount { get; set; }

    public decimal CashSalesUSD { get; set; }
    public decimal CashSalesCDF { get; set; }
    public decimal CashSalesEUR { get; set; }
    public decimal CashSalesCNY { get; set; }

    public decimal CashRefundsUSD { get; set; }
    public decimal CashRefundsCDF { get; set; }
    public decimal CashRefundsEUR { get; set; }
    public decimal CashRefundsCNY { get; set; }

    public decimal ExpectedCashUSD { get; set; }
    public decimal ExpectedCashCDF { get; set; }
    public decimal ExpectedCashEUR { get; set; }
    public decimal ExpectedCashCNY { get; set; }

    public decimal NonCashTotal { get; set; }
}

internal class CashFlowByCurrency
{
    public decimal salesUSD, salesCDF, salesEUR, salesCNY;
    public decimal refundsUSD, refundsCDF, refundsEUR, refundsCNY;
}