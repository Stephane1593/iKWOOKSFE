using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class ReportListItemViewModel : ObservableObject
{
    public int ReportId { get; init; }
    public ReportType Type { get; init; }
    public int ReportNumber { get; init; }
    public bool IsPeriodic { get; init; }
    public DateTime GeneratedAt { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public string OperatorName { get; init; } = "";
    public decimal GrandTotalTTC { get; init; }
    public decimal GrandTotalHT { get; init; }
    public decimal GrandTotalTVA { get; init; }
    public int TotalInvoiceCount { get; init; }
    public int IncompleteCount { get; init; }
    public string? PrintContent { get; init; }

    // ══════ DISPLAY ══════

    public string TypeBadge => Type.ToString();

    public string TypeLabel => Type switch
    {
        ReportType.Z => "Z-Rapport",
        ReportType.X => IsPeriodic ? "X-Périodique" : "X-Quotidien",
        ReportType.A => "A-Rapport",
        _ => Type.ToString()
    };

    public string TypeColor => Type switch
    {
        ReportType.Z => "#DC2626",
        ReportType.X => "#2563EB",
        ReportType.A => "#059669",
        _ => "#64748B"
    };

    public string TypeBgColor => Type switch
    {
        ReportType.Z => "#FEF2F2",
        ReportType.X => "#EFF6FF",
        ReportType.A => "#ECFDF5",
        _ => "#F1F5F9"
    };

    public string Title => $"{TypeLabel} N°{ReportNumber}";
    public string DateLabel => GeneratedAt.ToString("dd/MM/yyyy HH:mm");
    public string PeriodLabel => PeriodStart.Date == PeriodEnd.Date
        ? $"{PeriodStart:dd/MM/yyyy}"
        : $"{PeriodStart:dd/MM} → {PeriodEnd:dd/MM/yyyy}";
    public string TotalLabel => $"{GrandTotalTTC:N0} CDF";
    public string InvoiceCountLabel => TotalInvoiceCount == 1
        ? "1 facture"
        : $"{TotalInvoiceCount} factures";

    public static ReportListItemViewModel FromEntity(DailyReport r) => new()
    {
        ReportId = r.Id,
        Type = r.Type,
        ReportNumber = r.ReportNumber,
        IsPeriodic = r.IsPeriodic,
        GeneratedAt = r.GeneratedAt,
        PeriodStart = r.PeriodStart,
        PeriodEnd = r.PeriodEnd,
        OperatorName = r.OperatorName,
        GrandTotalTTC = r.GrandTotalTTC,
        GrandTotalHT = r.GrandTotalHT,
        GrandTotalTVA = r.GrandTotalTVA,
        TotalInvoiceCount = r.TotalInvoiceCount,
        IncompleteCount = r.IncompleteCount,
        PrintContent = r.PrintContent
    };
}