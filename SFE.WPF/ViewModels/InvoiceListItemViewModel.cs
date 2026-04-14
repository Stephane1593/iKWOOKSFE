using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class InvoiceListItemViewModel : ObservableObject
{
    [ObservableProperty] private int _invoiceId;
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private string _typeLabel = "";
    [ObservableProperty] private string _typeColor = "";
    [ObservableProperty] private string _statusLabel = "";
    [ObservableProperty] private string _statusIcon = "";
    [ObservableProperty] private string _statusColor = "";
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private string _dateDisplay = "";
    [ObservableProperty] private string _timeDisplay = "";
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private string _totalDisplay = "";
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _codeDEFShort = "";
    [ObservableProperty] private string _operatorName = "";
    [ObservableProperty] private string _clientName = "";
    [ObservableProperty] private int _lineCount;
    [ObservableProperty] private string _paymentIcon = "";

    public static InvoiceListItemViewModel FromEntity(Invoice invoice)
    {
        var (typeLabel, typeColor) = GetTypeDisplay(invoice.Type);
        var (statusLabel, statusIcon, statusColor) = GetStatusDisplay(invoice.Status);
        var codeDef = invoice.CodeDEFDGI ?? "";

        return new InvoiceListItemViewModel
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            TypeLabel = typeLabel,
            TypeColor = typeColor,
            StatusLabel = statusLabel,
            StatusIcon = statusIcon,
            StatusColor = statusColor,
            CreatedAt = invoice.CreatedAt,
            DateDisplay = invoice.CreatedAt.ToString("dd/MM/yyyy"),
            TimeDisplay = invoice.CreatedAt.ToString("HH:mm"),
            TotalTTC = invoice.TotalTTC,
            TotalDisplay = $"{invoice.TotalTTC:N0} CDF",
            CodeDEFDGI = codeDef,
            CodeDEFShort = codeDef.Length > 16 ? codeDef[..16] + "…" : codeDef,
            OperatorName = invoice.OperatorName,
            ClientName = invoice.ClientName ?? "—",
            LineCount = invoice.Lines.Count,
            PaymentIcon = GetPaymentIcon(invoice.Payments.FirstOrDefault()?.PaymentType)
        };
    }

    private static (string label, string color) GetTypeDisplay(InvoiceType type) => type switch
    {
        InvoiceType.FV => ("FV", "#3B82F6"),
        InvoiceType.FT => ("FT", "#8B5CF6"),
        InvoiceType.EV => ("EV", "#EF4444"),
        InvoiceType.ET => ("ET", "#F97316"),
        _ => (type.ToString(), "#6B7280")
    };

    private static (string label, string icon, string color) GetStatusDisplay(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Normalized => ("Normalisée", "✓", "#10B981"),
        InvoiceStatus.Draft => ("Brouillon", "✎", "#F59E0B"),
        InvoiceStatus.Cancelled => ("Annulée", "✕", "#EF4444"),
        InvoiceStatus.Error => ("Erreur", "⚠", "#EF4444"),
        _ => (status.ToString(), "?", "#6B7280")
    };

    private static string GetPaymentIcon(PaymentType? type) => type switch
    {
        PaymentType.Especes => "💵",
        PaymentType.CarteBancaire => "💳",
        PaymentType.Virement => "🏦",
        PaymentType.MobileMoney => "📱",
        PaymentType.Cheques => "📝",
        PaymentType.Credit => "📋",
        PaymentType.Autre => "💰",
        _ => "💰"
    };
}