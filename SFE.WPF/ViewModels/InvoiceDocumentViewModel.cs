using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public partial class InvoiceDocumentViewModel : ObservableObject
{
    // ═══════ INVOICE CORE ═══════
    [ObservableProperty] private int _invoiceId;
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _typeName = "";
    [ObservableProperty] private string _typeTitle = "";
    [ObservableProperty] private InvoiceType _type;
    [ObservableProperty] private InvoiceStatus _status;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private string _createdAtDisplay = "";
    [ObservableProperty] private string _operatorName = "";
    [ObservableProperty] private string _isf = "";
    [ObservableProperty] private string _priceModeLabel = "MODE PRIX TTC";

    // ═══════ CLIENT ═══════
    [ObservableProperty] private string _clientName = "—";
    [ObservableProperty] private string _clientNIF = "—";
    [ObservableProperty] private string _clientType = "";
    [ObservableProperty] private string _clientContact = "—";
    [ObservableProperty] private string _clientAddress = "—";

    // ═══════ COMPANY ═══════
    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _companyNIF = "";
    [ObservableProperty] private string _companyISF = "";
    [ObservableProperty] private string _companyRCCM = "";
    [ObservableProperty] private string _companyIdNat = "";
    [ObservableProperty] private string _companyAddress = "";
    [ObservableProperty] private string _companyCity = "";
    [ObservableProperty] private string _companyPhone = "";
    [ObservableProperty] private string _companyEmail = "";
    [ObservableProperty] private string _companyFullAddress = "";
    [ObservableProperty] private ImageSource? _companyLogo;

    // ═══════ POINT OF SALE ═══════
    [ObservableProperty] private string _posName = "";
    [ObservableProperty] private string _posAddress = "";

    // ═══════ TOTALS ═══════
    [ObservableProperty] private decimal _totalHT;
    [ObservableProperty] private decimal _totalTVA;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private decimal _totalSpecificTax;
    [ObservableProperty] private int _lineCount;

    // ═══════ EXCHANGE ═══════
    [ObservableProperty] private decimal _exchangeRate;
    [ObservableProperty] private string _exchangeRateDisplay = "";
    [ObservableProperty] private decimal _totalTTCUsd;
    [ObservableProperty] private string _totalTTCUsdDisplay = "";
    [ObservableProperty] private bool _hasExchangeRate;

    // ═══════ NORMALIZATION ═══════
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _defNid = "";
    [ObservableProperty] private string _nim = "";
    [ObservableProperty] private string _counters = "";
    [ObservableProperty] private DateTime? _normalizedAt;
    [ObservableProperty] private string _normalizedAtDisplay = "";
    [ObservableProperty] private ImageSource? _qrCodeImage;
    [ObservableProperty] private bool _hasNormalization;

    // ═══════ AMOUNT IN WORDS ═══════
    [ObservableProperty] private string _amountInWords = "";

    // ═══════ SOURCE DATA (for native PDF generation) ═══════
    public Invoice? SourceInvoice { get; set; }
    public Company? SourceCompany { get; set; }
    public PointOfSale? SourcePos { get; set; }
    public decimal SourceExchangeRate { get; set; }
    public byte[]? SourceLogoBytes { get; set; }

    // ═══════ COLLECTIONS ═══════
    public ObservableCollection<DocLineViewModel> Lines { get; } = new();
    public ObservableCollection<DocPaymentViewModel> Payments { get; } = new();
    public ObservableCollection<TaxBreakdownLine> TaxBreakdown { get; } = new();

    // Add inside InvoiceDocumentViewModel class
    public int PrintNumber { get; set; } = 1;

    // ═══════════════════════════════════════════════════════
    //  FACTORY
    // ═══════════════════════════════════════════════════════

    public static InvoiceDocumentViewModel Create(
        Invoice invoice,
        Company? company,
        ITimeProvider timeProvider,
        PointOfSale? pos = null,
        decimal exchangeRate = 0)
    {
        var createdLocal = timeProvider.ToLocal(invoice.CreatedAt);
        var normalizedLocal = invoice.NormalizedAt is { } n
            ? timeProvider.ToLocal(n)
            : (DateTimeOffset?)null;

        var vm = new InvoiceDocumentViewModel
        {
            // Invoice
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Type = invoice.Type,
            TypeName = GetTypeName(invoice.Type),
            TypeTitle = GetTypeTitle(invoice.Type),
            Status = invoice.Status,
            CreatedAt = createdLocal.DateTime,
            CreatedAtDisplay = createdLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            OperatorName = invoice.OperatorName,
            Isf = invoice.ISF ?? "",

            // Client
            ClientName = invoice.ClientName ?? "—",
            ClientNIF = invoice.ClientNIF ?? "—",
            ClientType = invoice.ClientType.ToString(),
            ClientContact = invoice.ClientPhone ?? "—",
            ClientAddress = invoice.ClientAddress ?? "—",

            // Totals
            TotalHT = invoice.TotalHT,
            TotalTVA = invoice.TotalTVA,
            TotalTTC = invoice.TotalTTC,
            TotalSpecificTax = invoice.TotalSpecificTax,
            LineCount = invoice.Lines.Count,

            // Normalization
            CodeDEFDGI = invoice.CodeDEFDGI ?? "",
            Nim = invoice.NIM ?? "",
            Counters = invoice.Counters ?? "",
            NormalizedAt = normalizedLocal?.DateTime,
            NormalizedAtDisplay = normalizedLocal?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? "—",
            HasNormalization = !string.IsNullOrEmpty(invoice.CodeDEFDGI),
        };

        // Company
        if (company != null)
        {
            vm.CompanyName = company.Name;
            vm.CompanyNIF = company.NIF;
            vm.CompanyISF = company.ISF;
            vm.CompanyRCCM = company.RCCM;
            vm.CompanyAddress = company.Address;
            vm.CompanyCity = company.City;
            vm.CompanyPhone = company.Phone;
            vm.CompanyEmail = company.Email;
            vm.CompanyFullAddress = BuildFullAddress(company);
            vm.CompanyLogo = BytesToImage(company.Logo);
            vm.PriceModeLabel = company.DefaultPriceMode == PriceMode.HT
                ? "MODE PRIX HT" : "MODE PRIX TTC";
        }

        // POS
        if (pos != null)
        {
            vm.PosName = pos.Name;
            vm.PosAddress = $"{pos.Address}, {pos.City}".Trim(' ', ',');
        }

        // Exchange rate
        if (exchangeRate > 0)
        {
            vm.ExchangeRate = exchangeRate;
            vm.ExchangeRateDisplay = exchangeRate.ToString("N4");
            vm.TotalTTCUsd = Math.Round(invoice.TotalTTC / exchangeRate, 2);
            vm.TotalTTCUsdDisplay = vm.TotalTTCUsd.ToString("N2");
            vm.HasExchangeRate = true;
        }

        // QR Code
        vm.QrCodeImage = QrCodeHelper.Generate(invoice.QRCodeContent);

        // Amount in words
        vm.AmountInWords = $"Arrêté la présente facture à la somme de {NumberToFrenchWords.Convert(invoice.TotalTTC)}";

        // Lines
        int num = 1;
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            vm.Lines.Add(new DocLineViewModel
            {
                Number = num++,
                Code = line.Code,
                Name = line.Name,
                TaxGroupLabel = FormatTaxGroupLabel(line.TaxGroup, invoice.Type),
                UnitPriceHT = line.UnitPriceHT,
                TaxSpecific = line.SpecificTaxRate > 0 ? $"{line.SpecificTaxRate}%" : "",
                Quantity = line.Quantity,
                TotalHT = line.AmountHT
            });
        }

        // Payments
        decimal paymentTotal = 0;
        foreach (var pay in invoice.Payments)
        {
            vm.Payments.Add(new DocPaymentViewModel
            {
                Label = GetPaymentLabel(pay.PaymentType),
                Amount = pay.Amount
            });
            paymentTotal += pay.Amount;
        }
        vm.Payments.Add(new DocPaymentViewModel
        {
            Label = "",
            Amount = paymentTotal,
            IsTotal = true
        });

        // Tax breakdown
        BuildTaxBreakdown(vm, invoice);

        // ═══════════════════════════════════════════════════════
        // SOURCE DATA — for InvoicePrinterHelper / QuestPDF
        // ═══════════════════════════════════════════════════════
        vm.SourceInvoice = invoice;
        vm.SourceCompany = company;
        vm.SourcePos = pos;
        vm.SourceExchangeRate = exchangeRate;
        vm.SourceLogoBytes = company?.Logo;

        return vm;
    }

    // ═══════════════════════════════════════════════════════
    //  TAX BREAKDOWN
    // ═══════════════════════════════════════════════════════

    private static void BuildTaxBreakdown(InvoiceDocumentViewModel vm, Invoice invoice)
    {
        foreach (var grp in invoice.Lines
                     .GroupBy(l => l.TaxGroup)
                     .OrderBy(g => g.Key))
        {
            var tg = grp.Key;
            char letter = (char)('A' + (int)tg);
            decimal rate = grp.First().TaxRate;
            string desc = GetTaxGroupFullDesc(tg);

            if (tg == TaxGroup.A)
            {
                vm.TaxBreakdown.Add(new TaxBreakdownLine
                {
                    Label = "EXONERES ET HORS CHAMP",
                    Amount = grp.Sum(l => l.AmountHT)
                });
                continue;
            }

            // H.T.
            decimal ht = grp.Sum(l => l.AmountHT);
            if (ht != 0)
            {
                vm.TaxBreakdown.Add(new TaxBreakdownLine
                {
                    Label = $"H.T [{letter}] {desc} {rate:N2}%",
                    Amount = ht
                });
            }

            // TVA
            decimal tva = grp.Sum(l => l.AmountTVA);
            if (tva != 0)
            {
                vm.TaxBreakdown.Add(new TaxBreakdownLine
                {
                    Label = $"TVA [{letter}] {desc} {rate:N2}%",
                    Amount = tva
                });
            }

            // Specific tax
            decimal specific = grp.Sum(l => l.TaxSpecificAmount);
            if (specific != 0)
            {
                vm.TaxBreakdown.Add(new TaxBreakdownLine
                {
                    Label = $"T.S. [{letter}] {desc} {rate:N2}%",
                    Amount = specific
                });
            }
        }

        // Total specific tax if any
        if (invoice.TotalSpecificTax > 0)
        {
            vm.TaxBreakdown.Add(new TaxBreakdownLine
            {
                Label = "Total [N] TVA spécifique",
                Amount = invoice.TotalSpecificTax
            });
        }
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    private static string BuildFullAddress(Company c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Address)) parts.Add(c.Address);
        if (!string.IsNullOrWhiteSpace(c.City)) parts.Add(c.City);
        if (!string.IsNullOrWhiteSpace(c.Phone)) parts.Add($"Tel: {c.Phone}");
        if (!string.IsNullOrWhiteSpace(c.Email)) parts.Add($"Email : {c.Email}");
        if (!string.IsNullOrWhiteSpace(c.RCCM)) parts.Add($"N° RCCM : {c.RCCM}");
        if (!string.IsNullOrWhiteSpace(c.ISF)) parts.Add($"Id. Nat. {c.ISF}");
        return string.Join("\n", parts);
    }

    private static string FormatTaxGroupLabel(TaxGroup tg, InvoiceType invType)
    {
        char letter = (char)('A' + (int)tg);
        string typeCode = invType switch
        {
            InvoiceType.FV => "BIE",
            InvoiceType.FT => "BIE",
            InvoiceType.EV => "EXP",
            InvoiceType.ET => "EXP",
            InvoiceType.FA => "AVO",
            InvoiceType.EA => "AVO",
            _ => "BIE"
        };
        return $"[{letter}][{typeCode}]";
    }

    private static string GetTypeTitle(InvoiceType type) => type switch
    {
        InvoiceType.FV => "FACTURE DE VENTE",
        InvoiceType.FT => "FACTURE D'ACOMPTE",
        InvoiceType.EV => "FACTURE DE VENTE À L'EXPORTATION",
        InvoiceType.ET => "FACTURE D'ACOMPTE À L'EXPORTATION",
        InvoiceType.FA => "FACTURE D'AVOIR",
        InvoiceType.EA => "FACTURE D'AVOIR À L'EXPORTATION",
        _ => "FACTURE"
    };

    private static string GetTypeName(InvoiceType type) => type switch
    {
        InvoiceType.FV => "Facture de Vente",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Facture de vente à l'exportation",
        InvoiceType.ET => "Facture d'acompte à l'exportation",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.EA => "Facture d'avoir à l'exportation",
        _ => type.ToString()
    };

    private static string GetPaymentLabel(PaymentType pt) => pt switch
    {
        PaymentType.Especes => "ESPECES",
        PaymentType.Virement => "VIREMENT",
        PaymentType.CarteBancaire => "CARTE BANCAIRE",
        PaymentType.MobileMoney => "MOBILEMONEY",
        PaymentType.Cheques => "CHEQUES",
        PaymentType.Credit => "CREDIT",
        PaymentType.Autre => "AUTRE",
        _ => pt.ToString().ToUpper()
    };

    private static string GetTaxGroupFullDesc(TaxGroup tg) => tg switch
    {
        TaxGroup.A => "Exonéré",
        TaxGroup.B => "Taxable",
        TaxGroup.C => "Taxable",
        TaxGroup.D => "Dérogatoire",
        TaxGroup.E => "Exportation",
        TaxGroup.F => "Marché public à financement extérieur",
        TaxGroup.G => "Marché public à financement extérieur",
        TaxGroup.H => "Consignation",
        TaxGroup.I => "Garantie",
        TaxGroup.J => "Débit",
        TaxGroup.K => "Non applicable",
        TaxGroup.L => "Précompte",
        TaxGroup.M => "Régime spécial",
        TaxGroup.N => "TVA spécifique",
        _ => ""
    };

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
}

// ═══════════════════════════════════════════════════════
//  SUB VIEW MODELS
// ═══════════════════════════════════════════════════════

public class DocLineViewModel
{
    public int Number { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TaxGroupLabel { get; set; } = "";
    public decimal UnitPriceHT { get; set; }
    public string TaxSpecific { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal TotalHT { get; set; }
}

public class DocPaymentViewModel
{
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsTotal { get; set; }
}

public class TaxBreakdownLine
{
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
}