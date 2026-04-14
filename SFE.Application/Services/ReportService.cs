using System.Text;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class ReportService
{
    private readonly IUnitOfWork _uow;

    public ReportService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    // ══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════

    public async Task<DailyReport> GenerateReportXAsync(string operatorName)
    {
        var lastZ = await GetLastReportDateAsync(ReportType.Z);
        var periodStart = lastZ ?? DateTime.Today;
        var periodEnd = DateTime.Now;

        var report = await BuildZXReportAsync(ReportType.X, periodStart, periodEnd, operatorName);
        report.IsPeriodic = false;
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.X);
        report.PrintContent = FormatZXReport(report);

        await SaveReportAsync(report);
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
        return report;
    }

    public async Task<DailyReport> GenerateReportZAsync(string operatorName)
    {
        var lastZ = await GetLastReportDateAsync(ReportType.Z);
        var periodStart = lastZ ?? DateTime.Today;
        var periodEnd = DateTime.Now;

        var report = await BuildZXReportAsync(ReportType.Z, periodStart, periodEnd, operatorName);
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.Z);
        report.PrintContent = FormatZXReport(report);

        await SaveReportAsync(report);
        return report;
    }

    public async Task<DailyReport> GenerateReportAAsync(string operatorName)
    {
        var lastA = await GetLastReportDateAsync(ReportType.A);
        var periodStart = lastA ?? DateTime.Today;
        var periodEnd = DateTime.Now;

        var report = await BuildAReportAsync(periodStart, periodEnd, operatorName);
        report.ReportNumber = await GetNextReportNumberAsync(ReportType.A);
        report.PrintContent = FormatAReport(report);

        await SaveReportAsync(report);
        return report;
    }

    // ══════════════════════════════════════════════════════════
    //  BUILD Z/X REPORT (§1.3)
    // ══════════════════════════════════════════════════════════

    private async Task<DailyReport> BuildZXReportAsync(
        ReportType type, DateTime start, DateTime end, string operatorName)
    {
        // ── Snapshot en-tête entreprise (from Company, not AppSettings) ──
        var company = await GetCompanyAsync();

        var normalized = await FetchInvoicesAsync(start, end, InvoiceStatus.Normalized);

        // §1.3.3.m — Ventes incomplètes
        int incompleteCount = 0;
        foreach (var status in new[] { InvoiceStatus.Draft, InvoiceStatus.Error, InvoiceStatus.Cancelled })
        {
            var incomplete = await _uow.Invoices.SearchAsync(
                new InvoiceSearchCriteria { DateFrom = start, DateTo = end, Status = status },
                1, int.MaxValue);
            incompleteCount += incomplete.TotalCount;
        }

        var report = new DailyReport
        {
            Type = type,
            PeriodStart = start,
            PeriodEnd = end,
            GeneratedAt = DateTime.Now,
            OperatorName = operatorName,
            CompanyName = company?.Name ?? "",
            CompanyNIF = company?.NIF ?? "",
            ISF = company?.ISF ?? "",
            TotalInvoiceCount = normalized.Count,
            TotalItemCount = normalized.Sum(i => i.Lines.Count),
            IncompleteCount = incompleteCount,
        };

        // §1.3.3.g — Totaux par type de facture
        // §1.3.3.i — Totaux par groupe de taxation par type de facture
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

        // §1.3.3.j,k — Ventilation des paiements
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

        // Totaux généraux nets (ventes − avoirs)
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

        var salesLines = normalized
            .Where(i => i.Type.IsSale())
            .SelectMany(i => i.Lines)
            .ToList();

        var creditLines = normalized
            .Where(i => i.Type.IsCreditNote())
            .SelectMany(i => i.Lines)
            .ToList();

        var allCodes = salesLines.Select(l => l.Code)
            .Union(creditLines.Select(l => l.Code))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var allProducts = (await _uow.GetRepository<Product>()
            .FindAsync(p => true)).ToList();
        var productByCode = allProducts
            .Where(p => !string.IsNullOrEmpty(p.Code))
            .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allPosStocks = (await _uow.GetRepository<PosStock>()
            .FindAsync(ps => true)).ToList();
        var totalStockByProductId = allPosStocks
            .GroupBy(ps => ps.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(ps => ps.Quantity));

        var report = new DailyReport
        {
            Type = ReportType.A,
            PeriodStart = start,
            PeriodEnd = end,
            GeneratedAt = DateTime.Now,
            OperatorName = operatorName,
            CompanyName = company?.Name ?? "",
            CompanyNIF = company?.NIF ?? "",
            ISF = company?.ISF ?? "",
        };

        foreach (var code in allCodes)
        {
            var sold = salesLines
                .Where(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var returned = creditLines
                .Where(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                .ToList();

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

        sb.AppendLine(thin);
        sb.AppendLine(Center("DÉTAIL PAR GROUPE DE TAXATION", W));
        sb.AppendLine(thin);

        var invoiceTypesInReport = r.TaxGroupDetails
            .Select(d => d.InvoiceType)
            .Distinct()
            .OrderBy(t => t);

        foreach (var invType in invoiceTypesInReport)
        {
            sb.AppendLine();
            sb.AppendLine($"  ┌─ {invType} — {invType.Label()}");
            sb.AppendLine($"  │ {"Grp",-6} {"Total",12} {"Taxable",12} {"TVA",12}");
            sb.AppendLine($"  │ {new string('─', W - 6)}");

            var details = r.TaxGroupDetails
                .Where(d => d.InvoiceType == invType)
                .OrderBy(d => d.TaxGroup);

            decimal subTotal = 0, subTaxable = 0, subTax = 0;

            foreach (var d in details)
            {
                char label = (char)('A' + (int)d.TaxGroup);
                string desc = GetTaxGroupShortDesc(d.TaxGroup);
                sb.AppendLine(
                    $"  │ {label} {desc,-4}" +
                    $" {d.TotalAmount,12:N2} {d.TaxableAmount,12:N2} {d.TaxAmount,12:N2}");

                subTotal += d.TotalAmount;
                subTaxable += d.TaxableAmount;
                subTax += d.TaxAmount;
            }

            sb.AppendLine($"  │ {new string('─', W - 6)}");
            sb.AppendLine(
                $"  │ {"TOTAL",-6}" +
                $" {subTotal,12:N2} {subTaxable,12:N2} {subTax,12:N2}");
            sb.AppendLine($"  └─");
        }
        sb.AppendLine();

        sb.AppendLine(thin);
        sb.AppendLine(Center("VENTILATION DES PAIEMENTS", W));
        sb.AppendLine(thin);
        sb.AppendLine($"  {"Mode de paiement",-26} {"Nb fact.",8} {"Montant",14}");
        sb.AppendLine($"  {new string('─', W - 4)}");

        int totalPaymentInvoices = 0;
        decimal totalPaymentAmount = 0;

        foreach (var ps in r.PaymentSummaries.OrderBy(p => p.PaymentType))
        {
            sb.AppendLine(
                $"  {GetPaymentLabel(ps.PaymentType),-26}" +
                $" {ps.InvoiceCount,8} {ps.TotalAmount,14:N2}");
            totalPaymentInvoices += ps.InvoiceCount;
            totalPaymentAmount += ps.TotalAmount;
        }

        sb.AppendLine($"  {new string('─', W - 4)}");
        sb.AppendLine($"  {"TOTAL",-26} {totalPaymentInvoices,8} {totalPaymentAmount,14:N2}");
        sb.AppendLine();

        var creditSummary = r.InvoiceTypeSummaries
            .Where(s => s.InvoiceType.IsCreditNote())
            .ToList();

        if (creditSummary.Any())
        {
            sb.AppendLine(thin);
            sb.AppendLine(Center("ÉLÉMENTS RÉDUCTEURS", W));
            sb.AppendLine(thin);
            foreach (var cn in creditSummary)
            {
                sb.AppendLine($"  {cn.InvoiceType.Label(),-30} {cn.Count,4} × {cn.TotalTTC,14:N2}");
            }
            sb.AppendLine();
        }

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
        sb.AppendLine(Center($"Imprimé le {DateTime.Now:dd/MM/yyyy HH:mm}", W));
        sb.AppendLine(Center($"ISF: {r.ISF}", W));

        return sb.ToString();
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
            $"  {"Code",-12} {"Désignation",-20}" +
            $" {"P.U.",10} {"Taux",6}" +
            $" {"Vendu",8} {"Retour",8} {"Stock",8}");
        sb.AppendLine(thin);

        foreach (var a in r.ArticleLines)
        {
            string name = a.ArticleName.Length > 20
                ? a.ArticleName[..17] + "..."
                : a.ArticleName;
            char grpChar = (char)('A' + (int)a.TaxGroup);
            string taxLabel = $"{grpChar}/{a.TaxRate:G}%";

            sb.AppendLine(
                $"  {a.ArticleCode,-12} {name,-20}" +
                $" {a.UnitPrice,10:N2} {taxLabel,6}" +
                $" {a.QuantitySold,8:N3} {a.QuantityReturned,8:N3} {a.QuantityInStock,8:N3}");
        }

        sb.AppendLine(thin);

        decimal totalSold = r.ArticleLines.Sum(a => a.QuantitySold);
        decimal totalReturned = r.ArticleLines.Sum(a => a.QuantityReturned);
        decimal totalAmountNet = r.ArticleLines.Sum(a => a.TotalAmount);

        sb.AppendLine();
        sb.AppendLine($"  {"Total articles distincts :",-40} {r.ArticleLines.Count,10}");
        sb.AppendLine($"  {"Total quantité vendue :",-40} {totalSold,10:N3}");
        sb.AppendLine($"  {"Total quantité retournée :",-40} {totalReturned,10:N3}");
        sb.AppendLine($"  {"Montant net (ventes − retours) :",-40} {totalAmountNet,10:N2} CDF");
        sb.AppendLine();

        sb.AppendLine(heavy);
        sb.AppendLine(Center($"Imprimé le {DateTime.Now:dd/MM/yyyy HH:mm}", W));
        sb.AppendLine(Center($"ISF: {r.ISF}", W));

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private async Task<List<Invoice>> FetchInvoicesAsync(
        DateTime start, DateTime end, InvoiceStatus status)
    {
        var result = await _uow.Invoices.SearchAsync(
            new InvoiceSearchCriteria
            {
                DateFrom = start,
                DateTo = end,
                Status = status
            }, 1, int.MaxValue);

        return result.Items;
    }

    /// <summary>
    /// 🆕 Reads company info directly from the Companies table
    /// instead of AppSettings.
    /// </summary>
    private async Task<Company?> GetCompanyAsync()
    {
        return await _uow.Companies.GetCurrentCompanyAsync();
    }

    private async Task SaveReportAsync(DailyReport report)
    {
        await _uow.GetRepository<DailyReport>().AddAsync(report);
        await _uow.SaveChangesAsync();
    }

    private async Task<DateTime?> GetLastReportDateAsync(ReportType type)
    {
        var reports = await _uow.GetRepository<DailyReport>()
            .FindAsync(r => r.Type == type);
        return reports
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefault()?.PeriodEnd;
    }

    private async Task<int> GetNextReportNumberAsync(ReportType type)
    {
        var reports = await _uow.GetRepository<DailyReport>()
            .FindAsync(r => r.Type == type);
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