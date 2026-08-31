using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Models;
using SFE.WPF.Services;

namespace SFE.WPF.ViewModels;

public abstract partial class BaseReportListViewModel : ObservableObject
{
    protected readonly IUnitOfWork _uow;
    protected readonly ReportService _reportService;
    protected readonly CashSessionState _sessionState;
    protected readonly IAuthService _authService;
    protected readonly ITimeProvider _timeProvider;

    // ── Abstract ──
    protected abstract ReportType ReportType { get; }
    protected abstract string TypePrefix { get; }

    // ── List ──
    [ObservableProperty] private ObservableCollection<ReportListItem> _reports = new();
    [ObservableProperty] private ReportListItem? _selectedReport;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasSelectedReport;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _lastGeneratedInfo = "Aucun rapport";

    // ══════════════════════════════════════════
    //  INLINE DETAIL PANEL
    // ══════════════════════════════════════════
    [ObservableProperty] private bool _showDetailPanel;
    [ObservableProperty] private string _detailTitle = "";
    [ObservableProperty] private string _detailSubtitle = "";
    [ObservableProperty] private string _detailContent = "";
    [ObservableProperty] private ReportListItem? _detailItem;

    // ── Status ──
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isStatusError;

    protected BaseReportListViewModel(
        IUnitOfWork uow,
        ReportService reportService,
        CashSessionState sessionState,
        IAuthService authService,
        ITimeProvider timeProvider)
    {
        _uow = uow;
        _reportService = reportService;
        _sessionState = sessionState;
        _authService = authService;
        _timeProvider = timeProvider;

        _ = LoadAsync();
    }

    // ═══════════════════════════════════════
    //  SELECTION
    // ═══════════════════════════════════════

    partial void OnSelectedReportChanged(ReportListItem? value)
    {
        HasSelectedReport = value != null;
    }

    // ═══════════════════════════════════════
    //  LOAD
    // ═══════════════════════════════════════

    public async Task LoadAsync()
    {
        IsLoading = true;
        ClearStatus();

        try
        {
            var all = await _uow.GetRepository<DailyReport>()
                .FindAsync(r => r.Type == ReportType);

            var ordered = all.OrderByDescending(r => r.GeneratedAt).ToList();

            Reports.Clear();
            foreach (var r in ordered)
            {
                Reports.Add(new ReportListItem
                {
                    Id = r.Id,
                    ReportNumber = r.ReportNumber,
                    GeneratedAt = r.GeneratedAt,
                    PeriodStart = r.PeriodStart,
                    PeriodEnd = r.PeriodEnd,
                    OperatorName = r.OperatorName,
                    ISF = r.ISF,
                    GrandTotalTTC = r.GrandTotalTTC,
                    TotalInvoiceCount = r.TotalInvoiceCount,
                    PrintContent = r.PrintContent,
                    HasSessionData = r.HasSessionData,
                    IsPeriodic = r.IsPeriodic,
                    TypePrefix = TypePrefix
                });
            }

            TotalCount = Reports.Count;
            IsEmpty = Reports.Count == 0;

            var last = ordered.FirstOrDefault();
            LastGeneratedInfo = last != null
                ? $"Dernier : {last.GeneratedAt.ToLocalTime():dd/MM/yyyy HH:mm} — {last.OperatorName}"
                : "Aucun rapport généré";
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur de chargement : {ex.Message}", true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ═══════════════════════════════════════
    //  COMMANDS
    // ═══════════════════════════════════════

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    /// <summary>
    /// Shows the report detail in the inline right-hand panel.
    /// </summary>
    [RelayCommand]
    private void ViewDetail(ReportListItem? item)
    {
        if (item?.PrintContent == null) return;

        DetailItem = item;
        DetailTitle = $"{TypePrefix}-Rapport N°{item.ReportNumber}";
        DetailSubtitle = $"{item.DateDisplay}  •  {item.OperatorName}  •  {item.TotalDisplay}";
        DetailContent = item.PrintContent;
        ShowDetailPanel = true;

        SelectedReport = item;
    }

    /// <summary>
    /// Closes the inline detail panel.
    /// </summary>
    [RelayCommand]
    private void CloseDetailPanel()
    {
        ShowDetailPanel = false;
        DetailItem = null;
        DetailContent = "";
    }

    /// <summary>
    /// Opens the detail in a pop-out dialog.
    /// </summary>
    [RelayCommand]
    private void PopOutDetail()
    {
        var item = DetailItem;
        if (item?.PrintContent == null) return;

        var dialog = new Views.Pages.ReportDetailDialog
        {
            ReportTitle = DetailTitle,
            ReportSubtitle = DetailSubtitle,
            ReportContent = item.PrintContent
        };

        var mainWin = System.Windows.Application.Current.MainWindow;
        if (mainWin != null) dialog.Owner = mainWin;
        dialog.ShowDialog();
    }

    /// <summary>
    /// Copy the detail content to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyDetail()
    {
        if (!string.IsNullOrEmpty(DetailContent))
        {
            Clipboard.SetText(DetailContent);
            ShowStatus("✓ Contenu copié dans le presse-papiers.", false);
        }
    }

    // ═══════════════════════════════════════
    //  PRINT — SINGLE OR ALL
    // ═══════════════════════════════════════

    /// <summary>
    /// Print a single report. Falls back to DetailItem → SelectedReport.
    /// </summary>
    [RelayCommand]
    private void PrintReport(ReportListItem? item)
    {
        item ??= DetailItem ?? SelectedReport;
        if (item?.PrintContent == null)
        {
            ShowStatus("Sélectionnez un rapport à imprimer.", true);
            return;
        }

        try
        {
            string title = $"{TypePrefix}-Rapport N°{item.ReportNumber}";

            string tempDir = Path.Combine(Path.GetTempPath(), "SFE_Reports");
            Directory.CreateDirectory(tempDir);
            CleanOldTempFiles(tempDir, TimeSpan.FromDays(1));

            // Filename timestamp — display-only, use LocalNow from the time provider.
            string tempPath = Path.Combine(tempDir,
                SanitizeFileName($"{title}_{_timeProvider.LocalNow:yyyyMMdd_HHmmss}.pdf"));

            GenerateSingleReportPdf(item.PrintContent, title, item, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            ShowStatus($"✓ {title} ouvert pour impression.", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'impression : {ex.Message}", true);
        }
    }

    /// <summary>
    /// Print ALL reports in the list as a single multi-page PDF.
    /// Each report starts on its own page.
    /// </summary>
    [RelayCommand]
    private void PrintAllReports()
    {
        var printable = Reports.Where(r => !string.IsNullOrEmpty(r.PrintContent)).ToList();

        if (printable.Count == 0)
        {
            ShowStatus("Aucun rapport à imprimer.", true);
            return;
        }

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SFE_Reports");
            Directory.CreateDirectory(tempDir);
            CleanOldTempFiles(tempDir, TimeSpan.FromDays(1));

            string fileName = SanitizeFileName(
                $"{TypePrefix}-TousRapports_{_timeProvider.LocalNow:yyyyMMdd_HHmmss}.pdf");
            string tempPath = Path.Combine(tempDir, fileName);

            GenerateMultiReportPdf(printable, tempPath);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            ShowStatus($"✓ {printable.Count} rapport(s) ouverts pour impression.", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'impression groupée : {ex.Message}", true);
        }
    }

    // ═══════════════════════════════════════
    //  EXPORT — SINGLE OR ALL
    // ═══════════════════════════════════════

    /// <summary>
    /// Export a single report via SaveFileDialog.
    /// </summary>
    [RelayCommand]
    private void ExportReport(ReportListItem? item)
    {
        item ??= DetailItem ?? SelectedReport;
        if (item?.PrintContent == null)
        {
            ShowStatus("Sélectionnez un rapport à exporter.", true);
            return;
        }

        try
        {
            string baseName = SanitizeFileName(
                $"{TypePrefix}-Rapport-{item.ReportNumber}_{item.GeneratedAt.ToLocalTime():yyyyMMdd_HHmm}");

            var dlg = new SaveFileDialog
            {
                FileName = baseName,
                DefaultExt = ".pdf",
                Filter = "Document PDF (*.pdf)|*.pdf|Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

            if (ext == ".txt")
            {
                File.WriteAllText(dlg.FileName, item.PrintContent);
            }
            else
            {
                string title = $"{TypePrefix}-Rapport N°{item.ReportNumber}";
                GenerateSingleReportPdf(item.PrintContent, title, item, dlg.FileName);
            }

            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            ShowStatus($"✓ Exporté : {Path.GetFileName(dlg.FileName)}", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'export : {ex.Message}", true);
        }
    }

    /// <summary>
    /// Export ALL reports in the list as a single PDF or concatenated TXT.
    /// </summary>
    [RelayCommand]
    private void ExportAllReports()
    {
        var printable = Reports.Where(r => !string.IsNullOrEmpty(r.PrintContent)).ToList();

        if (printable.Count == 0)
        {
            ShowStatus("Aucun rapport à exporter.", true);
            return;
        }

        try
        {
            string baseName = SanitizeFileName(
                $"{TypePrefix}-TousRapports_{_timeProvider.LocalNow:yyyyMMdd_HHmm}");

            var dlg = new SaveFileDialog
            {
                FileName = baseName,
                DefaultExt = ".pdf",
                Filter = "Document PDF (*.pdf)|*.pdf|Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

            if (ext == ".txt")
            {
                ExportAllAsTxt(printable, dlg.FileName);
            }
            else
            {
                GenerateMultiReportPdf(printable, dlg.FileName);
            }

            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            ShowStatus($"✓ {printable.Count} rapport(s) exportés : {Path.GetFileName(dlg.FileName)}", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'export groupé : {ex.Message}", true);
        }
    }

    // ═══════════════════════════════════════
    //  PDF GENERATION (QuestPDF)
    // ═══════════════════════════════════════

    /// <summary>
    /// Generates a single-report PDF.
    /// </summary>
    private void GenerateSingleReportPdf(
        string textContent,
        string title,
        ReportListItem item,
        string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginVertical(30);
                page.MarginHorizontal(35);
                page.DefaultTextStyle(ts => ts.FontSize(8));

                // ── Header ──
                page.Header().Column(col =>
                {
                    col.Item().Text(title)
                        .FontSize(13).Bold()
                        .FontColor(Colors.Grey.Darken3);

                    col.Item().PaddingTop(2).Text(text =>
                    {
                        text.Span($"{item.DateDisplay}  •  {item.OperatorName}")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    col.Item().PaddingTop(6)
                        .LineHorizontal(0.5f)
                        .LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingBottom(8);
                });

                // ── Content ──
                page.Content().Text(textContent)
                    .FontFamily("Courier New")
                    .FontSize(8)
                    .LineHeight(1.35f);

                // ── Footer ──
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(ts => ts.FontSize(7).FontColor(Colors.Grey.Medium));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        })
        .GeneratePdf(outputPath);
    }

    /// <summary>
    /// Generates a multi-report PDF. Each report gets its own page section
    /// with a distinct header, and page numbering is continuous across all.
    /// </summary>
    private void GenerateMultiReportPdf(
        IReadOnlyList<ReportListItem> items,
        string outputPath)
    {
        Document.Create(container =>
        {
            foreach (var item in items)
            {
                string title = $"{TypePrefix}-Rapport N°{item.ReportNumber}";

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginVertical(30);
                    page.MarginHorizontal(35);
                    page.DefaultTextStyle(ts => ts.FontSize(8));

                    // ── Header (per-report) ──
                    page.Header().Column(col =>
                    {
                        col.Item().Text(title)
                            .FontSize(13).Bold()
                            .FontColor(Colors.Grey.Darken3);

                        col.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span($"{item.DateDisplay}  •  {item.OperatorName}")
                                .FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        col.Item().PaddingTop(6)
                            .LineHorizontal(0.5f)
                            .LineColor(Colors.Grey.Lighten2);

                        col.Item().PaddingBottom(8);
                    });

                    // ── Content ──
                    page.Content().Text(item.PrintContent ?? "")
                        .FontFamily("Courier New")
                        .FontSize(8)
                        .LineHeight(1.35f);

                    // ── Footer — continuous page numbering ──
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(ts => ts.FontSize(7).FontColor(Colors.Grey.Medium));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }
        })
        .GeneratePdf(outputPath);
    }

    /// <summary>
    /// Concatenates all reports into a single TXT file separated by dividers.
    /// </summary>
    private void ExportAllAsTxt(IReadOnlyList<ReportListItem> items, string outputPath)
    {
        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            writer.WriteLine(new string('═', 60));
            writer.WriteLine($"  {TypePrefix}-Rapport N°{item.ReportNumber}");
            writer.WriteLine($"  {item.DateDisplay}  •  {item.OperatorName}");
            writer.WriteLine(new string('═', 60));
            writer.WriteLine();
            writer.WriteLine(item.PrintContent ?? "(Contenu vide)");

            if (i < items.Count - 1)
            {
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine(new string('-', 60));
                writer.WriteLine();
                writer.WriteLine();
            }
        }
    }

    // ═══════════════════════════════════════
    //  STATUS
    // ═══════════════════════════════════════

    protected void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        HasStatus = true;
    }

    protected void ClearStatus()
    {
        HasStatus = false;
        StatusMessage = "";
    }

    protected string GetOperatorName()
    {
        return _sessionState.Current?.OperatorName
               ?? _authService.CurrentUser?.FullName
               ?? "Inconnu";
    }

    // ═══════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    /// <summary>
    /// Deletes temp PDFs older than <paramref name="maxAge"/>.
    /// Uses UTC on both sides (time provider + file creation time) to be
    /// immune to DST transitions and local-clock tampering.
    /// </summary>
    private void CleanOldTempFiles(string directory, TimeSpan maxAge)
    {
        try
        {
            var cutoffUtc = _timeProvider.UtcNow.UtcDateTime - maxAge;
            foreach (var file in Directory.GetFiles(directory, "*.pdf"))
            {
                if (File.GetCreationTimeUtc(file) < cutoffUtc)
                    File.Delete(file);
            }
        }
        catch { /* non-critical */ }
    }
}