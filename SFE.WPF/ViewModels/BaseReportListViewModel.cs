using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SFE.Application.Interfaces;
using SFE.Application.Services;
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
        IAuthService authService)
    {
        _uow = uow;
        _reportService = reportService;
        _sessionState = sessionState;
        _authService = authService;

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
                ? $"Dernier : {last.GeneratedAt:dd/MM/yyyy HH:mm} — {last.OperatorName}"
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

        // Also select it in the grid
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
    /// Opens the detail in a pop-out dialog (the existing ReportDetailDialog).
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
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var doc = new FlowDocument(
                    new Paragraph(new Run(item.PrintContent)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10
                    }))
                {
                    PageWidth = printDialog.PrintableAreaWidth,
                    PagePadding = new Thickness(40)
                };

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"{TypePrefix}-Rapport N°{item.ReportNumber}");
                ShowStatus("✓ Rapport envoyé à l'imprimante.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'impression : {ex.Message}", true);
        }
    }

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
            var dlg = new SaveFileDialog
            {
                FileName = $"{TypePrefix}-Rapport-{item.ReportNumber}_{item.GeneratedAt:yyyyMMdd_HHmm}",
                DefaultExt = ".txt",
                Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, item.PrintContent);
                ShowStatus($"✓ Exporté : {Path.GetFileName(dlg.FileName)}", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur d'export : {ex.Message}", true);
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
}