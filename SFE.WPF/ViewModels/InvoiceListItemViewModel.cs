using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Globalization;
using System.Windows.Media;

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

    // ══════════════════════ TRAÇABILITÉ PROFORMA ══════════════════════
    [ObservableProperty] private int? _convertedToInvoiceId;
    [ObservableProperty] private DateTime? _proformaValidUntil;
    [ObservableProperty] private int? _sourceProformaId;


    [ObservableProperty] private int _printCount;
    [ObservableProperty] private DateTime? _firstPrintedAt;
    [ObservableProperty] private DateTime? _lastPrintedAt;
    public bool IsProforma => Type == InvoiceType.PRO;

    private DateOnly _todayLocal;   // set by the factory
    public bool IsConvertibleProforma =>
        Type == InvoiceType.PRO
        && !ConvertedToInvoiceId.HasValue
        && Status != InvoiceStatus.Cancelled
        && (ProformaValidUntil == null
            || DateOnly.FromDateTime(ProformaValidUntil.Value) >= _todayLocal);

    public bool IsExpiredProforma =>
        Type == InvoiceType.PRO
        && ProformaValidUntil.HasValue
        && DateOnly.FromDateTime(ProformaValidUntil.Value) < _todayLocal
        && !ConvertedToInvoiceId.HasValue;

    public string ProformaValidityDisplay =>
        ProformaValidUntil.HasValue
            ? $"Valable jusqu'au {ProformaValidUntil.Value:dd/MM/yyyy}"
            : "Sans date d'expiration";

    public string PrintTooltip => PrintCount switch
    {
        0 => "Jamais imprimée",
        1 => $"Original imprimé le {FirstPrintedAt:dd/MM/yyyy HH:mm}",
        _ => $"{PrintCount} tirages — dernier le {LastPrintedAt:dd/MM/yyyy HH:mm}"
    };

    public string PrintBadgeText => PrintCount switch
    {
        0 => "—",
        1 => "ORIG",
        _ => $"DUP×{PrintCount - 1}"
    };

    public string PrintBadgeBrush => PrintCount switch
    {
        0 => "#9E9E9E",   // grey
        1 => "#2E7D32",   // green
        _ => "#C62828"    // red
    };
    public Brush PrintBadgeFg => PrintCount switch
    {
        0 => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),  // slate-400
        1 => new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),  // emerald-600
        _ => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))   // red-700
    };

    public Brush PrintBadgeBg => PrintCount switch
    {
        0 => new SolidColorBrush(Color.FromArgb(0x14, 0x94, 0xA3, 0xB8)),  // slate tint
        1 => new SolidColorBrush(Color.FromArgb(0x14, 0x05, 0x96, 0x69)),  // emerald tint
        _ => new SolidColorBrush(Color.FromArgb(0x14, 0xC6, 0x28, 0x28))   // red tint
    };

    // ═════════════════════════════════════════════════════
    //  FACTORY
    // ═════════════════════════════════════════════════════

    public static InvoiceListItemViewModel FromEntity(Invoice invoice, ITimeProvider timeProvider)
    {
        var (typeLabel, typeColor, typeBg) = GetTypeDisplay(invoice.Type);
        var (statusLabel, statusIcon, statusColor, statusBg) = GetStatusDisplay(invoice.Status);
        var codeDef = invoice.CodeDEFDGI ?? "";

        var createdLocal = timeProvider.ToLocal(invoice.CreatedAt);
        var validUntilLocal = invoice.ProformaValidUntil is { } v ? timeProvider.ToLocal(v) : (DateTimeOffset?)null;
        var firstPrintLocal = invoice.FirstPrintedAt is { } f ? timeProvider.ToLocal(f) : (DateTimeOffset?)null;
        var lastPrintLocal = invoice.LastPrintedAt is { } l ? timeProvider.ToLocal(l) : (DateTimeOffset?)null;

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

            CreatedAt = createdLocal.DateTime,
            DateDisplay = createdLocal.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            TimeDisplay = createdLocal.ToString("HH:mm", CultureInfo.InvariantCulture),

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
            PaymentIcon = GetPaymentIcon(invoice.Payments?.FirstOrDefault()?.PaymentType),

            ConvertedToInvoiceId = invoice.ConvertedToInvoiceId,
            ProformaValidUntil = validUntilLocal?.DateTime,
            SourceProformaId = invoice.SourceProformaId,

            PrintCount = invoice.PrintCount,
            FirstPrintedAt = firstPrintLocal?.DateTime,
            LastPrintedAt = lastPrintLocal?.DateTime,

            // 🆕 captured once, used by the expiry computed properties
            _todayLocal = DateOnly.FromDateTime(timeProvider.LocalNow.LocalDateTime)
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
        InvoiceType.PRO => ("PRO", "#546E7A", "#ECEFF1"),  // 🆕 grey — non-fiscal
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
        InvoiceStatus.Converted => ("Convertie", "↻", "#7C3AED", "#F5F3FF"),
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