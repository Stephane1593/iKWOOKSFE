using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class InvoiceDetailViewModel : ObservableObject
{
    [ObservableProperty] private int _invoiceId;
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _typeName = "";
    [ObservableProperty] private InvoiceType _type;
    [ObservableProperty] private InvoiceStatus _status;
    [ObservableProperty] private string _statusLabel = "";
    [ObservableProperty] private string _statusColor = "";

    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private string _createdAtDisplay = "";
    [ObservableProperty] private string _operatorName = "";
    [ObservableProperty] private string _isf = "";

    [ObservableProperty] private string _clientName = "";
    [ObservableProperty] private string _clientNIF = "";
    [ObservableProperty] private string _clientType = "";

    [ObservableProperty] private decimal _totalHT;
    [ObservableProperty] private decimal _totalTVA;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private int _lineCount;

    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _nim = "";
    [ObservableProperty] private string _counters = "";
    [ObservableProperty] private string _qrCodeContent = "";
    [ObservableProperty] private DateTime? _normalizedAt;
    [ObservableProperty] private string _normalizedAtDisplay = "";

    [ObservableProperty] private string _paymentSummary = "";

    public ObservableCollection<InvoiceLineDetailViewModel> Lines { get; } = new();
    public ObservableCollection<InvoicePaymentDetailViewModel> Payments { get; } = new();

    public static InvoiceDetailViewModel FromEntity(Invoice invoice)
    {
        var vm = new InvoiceDetailViewModel
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Type = invoice.Type,
            TypeName = GetTypeName(invoice.Type),
            Status = invoice.Status,
            StatusLabel = GetStatusLabel(invoice.Status),
            StatusColor = GetStatusColor(invoice.Status),
            CreatedAt = invoice.CreatedAt,
            CreatedAtDisplay = invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
            OperatorName = invoice.OperatorName,
            Isf = invoice.ISF,
            ClientName = invoice.ClientName ?? "—",
            ClientNIF = invoice.ClientNIF ?? "—",
            ClientType = invoice.ClientType.ToString(),
            TotalHT = invoice.TotalHT,
            TotalTVA = invoice.TotalTVA,
            TotalTTC = invoice.TotalTTC,
            CodeDEFDGI = invoice.CodeDEFDGI ?? "",
            Nim = invoice.NIM ?? "",
            Counters = invoice.Counters ?? "",
            QrCodeContent = invoice.QRCodeContent ?? "",
            NormalizedAt = invoice.NormalizedAt,
            NormalizedAtDisplay = invoice.NormalizedAt?.ToString("dd/MM/yyyy HH:mm:ss") ?? "—",
            LineCount = invoice.Lines.Count
        };

        int num = 1;
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            vm.Lines.Add(new InvoiceLineDetailViewModel
            {
                LineNumber = num++,
                Code = line.Code,
                Name = line.Name,
                TaxGroup = line.TaxGroup.ToString(),
                TaxRate = line.TaxRate,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                Unit = line.Unit,
                AmountHT = line.AmountHT,
                AmountTVA = line.AmountTVA,
                AmountTTC = line.AmountTTC
            });
        }

        foreach (var payment in invoice.Payments)
        {
            vm.Payments.Add(new InvoicePaymentDetailViewModel
            {
                PaymentType = payment.PaymentType.ToString(),
                Amount = payment.Amount
            });
        }

        vm.PaymentSummary = string.Join(", ",
            invoice.Payments.Select(p => $"{p.PaymentType}: {p.Amount:N0} CDF"));

        return vm;
    }

    private static string GetTypeName(InvoiceType type) => type switch
    {
        InvoiceType.FV => "Facture de Vente",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Facture de vente a l'exportation",
        InvoiceType.ET => "Facture d'acompte a l'exportation",
        InvoiceType.EA => "Facture d'avoir a l'exportation",
        InvoiceType.FA => "Facture d'avaoir",
        _ => type.ToString()
    };

    private static string GetStatusLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Brouillon",
        InvoiceStatus.Normalized => "Normalisée",
        InvoiceStatus.Cancelled => "Annulée",
        InvoiceStatus.Error => "Erreur",
        _ => status.ToString()
    };

    private static string GetStatusColor(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Normalized => "#10B981",
        InvoiceStatus.Draft => "#F59E0B",
        InvoiceStatus.Cancelled => "#EF4444",
        InvoiceStatus.Error => "#EF4444",
        _ => "#6B7280"
    };
}

public partial class InvoiceLineDetailViewModel : ObservableObject
{
    [ObservableProperty] private int _lineNumber;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _taxGroup = "";
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal _amountHT;
    [ObservableProperty] private decimal _amountTVA;
    [ObservableProperty] private decimal _amountTTC;

    public string QuantityDisplay => $"{Quantity:G} {Unit}";
    public string TaxDisplay => $"{TaxGroup} ({TaxRate}%)";
}

public partial class InvoicePaymentDetailViewModel : ObservableObject
{
    [ObservableProperty] private string _paymentType = "";
    [ObservableProperty] private decimal _amount;
}