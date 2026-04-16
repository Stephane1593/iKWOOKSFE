using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class InvoiceListItemViewModel : ObservableObject
{
    // ══════════════════════ IDENTITY ══════════════════════
    [ObservableProperty] private int _invoiceId;
    [ObservableProperty] private string _invoiceNumber = "";
    [ObservableProperty] private InvoiceType _type;
    [ObservableProperty] private InvoiceStatus _status;

    // ══════════════════════ TYPE DISPLAY ══════════════════
    [ObservableProperty] private string _typeLabel = "";
    [ObservableProperty] private string _typeColor = "";
    [ObservableProperty] private string _typeBadgeBg = "";

    // ══════════════════════ STATUS DISPLAY ════════════════
    [ObservableProperty] private string _statusLabel = "";
    [ObservableProperty] private string _statusIcon = "";
    [ObservableProperty] private string _statusColor = "";
    [ObservableProperty] private string _statusBadgeBg = "";

    // ══════════════════════ DATE ══════════════════════════
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private string _dateDisplay = "";
    [ObservableProperty] private string _timeDisplay = "";

    // ══════════════════════ AMOUNTS ═══════════════════════
    [ObservableProperty] private decimal _totalHT;
    [ObservableProperty] private decimal _totalTVA;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private string _totalDisplay = "";

    // ══════════════════════ CODES / REFS ══════════════════
    [ObservableProperty] private string _codeDEFDGI = "";
    [ObservableProperty] private string _codeDEFShort = "";

    // ══════════════════════ PEOPLE ════════════════════════
    [ObservableProperty] private string _operatorName = "";
    [ObservableProperty] private string _clientName = "";
    [ObservableProperty] private string _clientNIF = "";

    // ══════════════════════ MISC ═════════════════════════
    [ObservableProperty] private int _lineCount;
    [ObservableProperty] private string _paymentIcon = "";

    // ═════════════════════════════════════════════════════
    //  FACTORY
    // ═════════════════════════════════════════════════════

    public static InvoiceListItemViewModel FromEntity(Invoice invoice)
    {
        var (typeLabel, typeColor, typeBg) = GetTypeDisplay(invoice.Type);
        var (statusLabel, statusIcon, statusColor, statusBg) = GetStatusDisplay(invoice.Status);
        var codeDef = invoice.CodeDEFDGI ?? "";

        return new InvoiceListItemViewModel
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber ?? $"#{invoice.Id}",
            Type = invoice.Type,
            Status = invoice.Status,

            TypeLabel = typeLabel,
            TypeColor = typeColor,
            TypeBadgeBg = typeBg,

            StatusLabel = statusLabel,
            StatusIcon = statusIcon,
            StatusColor = statusColor,
            StatusBadgeBg = statusBg,

            CreatedAt = invoice.CreatedAt,
            DateDisplay = invoice.CreatedAt.ToString("dd/MM/yyyy"),
            TimeDisplay = invoice.CreatedAt.ToString("HH:mm"),

            TotalHT = invoice.TotalHT,
            TotalTVA = invoice.TotalTVA,
            TotalTTC = invoice.TotalTTC,
            TotalDisplay = $"{invoice.TotalTTC:N0} CDF",

            CodeDEFDGI = codeDef,
            CodeDEFShort = string.IsNullOrEmpty(codeDef)
                                  ? "—"
                                  : codeDef.Length > 16 ? codeDef[..16] + "…" : codeDef,

            OperatorName = invoice.OperatorName ?? "—",
            ClientName = invoice.ClientName ?? "Client comptoir",
            ClientNIF = invoice.ClientNIF ?? "—",

            LineCount = invoice.Lines?.Count ?? 0,
            PaymentIcon = GetPaymentIcon(invoice.Payments?.FirstOrDefault()?.PaymentType)
        };
    }

    // ═════════════════════════════════════════════════════
    //  TYPE — all 6 invoice types
    // ═════════════════════════════════════════════════════

    private static (string label, string color, string bg) GetTypeDisplay(InvoiceType type) => type switch
    {
        InvoiceType.FV => ("FV", "#3B82F6", "#DBEAFE"),   // Facture de vente        — blue
        InvoiceType.FA => ("FA", "#D97706", "#FEF3C7"),   // Facture d'avoir          — amber
        InvoiceType.FT => ("FT", "#8B5CF6", "#EDE9FE"),   // Facture d'acompte        — violet
        InvoiceType.EV => ("EV", "#EF4444", "#FEE2E2"),   // Vente export             — red
        InvoiceType.EA => ("EA", "#DC2626", "#FECACA"),   // Avoir export             — dark red
        InvoiceType.ET => ("ET", "#4F46E5", "#E0E7FF"),   // Acompte export           — indigo
        _ => (type.ToString(), "#64748B", "#F1F5F9")
    };

    // ═════════════════════════════════════════════════════
    //  STATUS — all statuses
    // ═════════════════════════════════════════════════════

    private static (string label, string icon, string color, string bg) GetStatusDisplay(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Normalized => ("Normalisée", "✓", "#059669", "#ECFDF5"),  // green
        InvoiceStatus.Draft => ("Brouillon", "✎", "#D97706", "#FFFBEB"),  // amber
        InvoiceStatus.Cancelled => ("Annulée", "✕", "#DC2626", "#FEF2F2"),  // red
        InvoiceStatus.Error => ("Erreur", "⚠", "#DC2626", "#FEF2F2"),  // red
        _ => (status.ToString(), "?", "#64748B", "#F1F5F9")
    };

    // ═════════════════════════════════════════════════════
    //  PAYMENT ICON
    // ═════════════════════════════════════════════════════

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