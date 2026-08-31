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

    // 🆕 DateTimeOffset — cohérent avec DailyReport & ITimeProvider
    public DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset PeriodStart { get; init; }
    public DateTimeOffset PeriodEnd { get; init; }

    public string OperatorName { get; init; } = "";
    public decimal GrandTotalTTC { get; init; }
    public decimal GrandTotalHT { get; init; }
    public decimal GrandTotalTVA { get; init; }
    public int TotalInvoiceCount { get; init; }
    public int IncompleteCount { get; init; }
    public string? PrintContent { get; init; }

    // 🆕 Champs session (Type=Z) — exposés pour binding direct
    public bool HasSessionData { get; init; }
    public decimal VarianceTotalCDF { get; init; }

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

    // .LocalDateTime garantit l'affichage en heure locale du poste
    public string DateLabel => GeneratedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

    public string PeriodLabel => PeriodStart.Date == PeriodEnd.Date
        ? $"{PeriodStart.LocalDateTime:dd/MM/yyyy}"
        : $"{PeriodStart.LocalDateTime:dd/MM} → {PeriodEnd.LocalDateTime:dd/MM/yyyy}";

    public string TotalLabel => $"{GrandTotalTTC:N0} CDF";

    public string InvoiceCountLabel => TotalInvoiceCount == 1
        ? "1 facture"
        : $"{TotalInvoiceCount} factures";

    // 🆕 Indicateur visuel d'écart de caisse (Z-rapport avec session)
    public bool HasVariance => HasSessionData && VarianceTotalCDF != 0;

    public string VarianceLabel => VarianceTotalCDF switch
    {
        0 => "",
        > 0 => $"+{VarianceTotalCDF:N0} CDF",
        _ => $"{VarianceTotalCDF:N0} CDF"
    };

    public string VarianceColor => VarianceTotalCDF switch
    {
        0 => "#64748B",
        > 0 => "#059669",   // vert = excédent
        _ => "#DC2626"     // rouge = manquant
    };

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
        PrintContent = r.PrintContent,
        HasSessionData = r.HasSessionData,
        VarianceTotalCDF = r.VarianceTotalCDF
    };
}