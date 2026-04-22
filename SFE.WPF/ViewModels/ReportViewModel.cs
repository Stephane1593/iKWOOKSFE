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

namespace SFE.WPF.ViewModels;

public partial class ReportViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;

    // ══════ LIST ══════
    public ObservableCollection<ReportListItemViewModel> Reports { get; } = new();

    [ObservableProperty] private ReportListItemViewModel? _selectedReport;

    // ══════ FILTER ══════
    [ObservableProperty] private string _selectedFilter = "Tous";
    public string[] FilterOptions { get; } =
        { "Tous", "Z-Rapport", "X-Rapport", "A-Rapport" };

    [ObservableProperty] private string _searchText = "";

    // ══════ STATS ══════
    [ObservableProperty] private int _zCount;
    [ObservableProperty] private int _xCount;
    [ObservableProperty] private int _aCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _lastZInfo = "Aucun";
    [ObservableProperty] private string _lastXInfo = "Aucun";
    [ObservableProperty] private string _lastAInfo = "Aucun";

    // ══════ PREVIEW ══════
    [ObservableProperty] private string _reportPreview = "";
    [ObservableProperty] private bool _showPreview;
    [ObservableProperty] private string _previewTitle = "";
    [ObservableProperty] private string _previewSubtitle = "";

    // ══════ STATE ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _noResults;

    /// <summary>All reports — source of truth for filtering.</summary>
    private List<ReportListItemViewModel> _allReports = new();

    public ReportViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        PageTitle = "Historique des rapports";

        _ = LoadAsync();
    }

    // ══════════════════════════════════════════════
    //  LOAD
    // ══════════════════════════════════════════════

    private async Task LoadAsync()
    {
        IsBusy = true;
        ClearStatus();

        try
        {
            var entities = await _unitOfWork.GetRepository<DailyReport>()
                .FindAsync(r => true);

            _allReports = entities
                .OrderByDescending(r => r.GeneratedAt)
                .Select(ReportListItemViewModel.FromEntity)
                .ToList();

            UpdateStats();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur de chargement : {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ══════════════════════════════════════════════
    //  STATS
    // ══════════════════════════════════════════════

    private void UpdateStats()
    {
        ZCount = _allReports.Count(r => r.Type == ReportType.Z);
        XCount = _allReports.Count(r => r.Type == ReportType.X);
        ACount = _allReports.Count(r => r.Type == ReportType.A);
        TotalCount = _allReports.Count;

        var lastZ = _allReports.FirstOrDefault(r => r.Type == ReportType.Z);
        var lastX = _allReports.FirstOrDefault(r => r.Type == ReportType.X);
        var lastA = _allReports.FirstOrDefault(r => r.Type == ReportType.A);

        LastZInfo = FormatLastInfo(lastZ);
        LastXInfo = FormatLastInfo(lastX);
        LastAInfo = FormatLastInfo(lastA);
    }

    private static string FormatLastInfo(ReportListItemViewModel? r)
        => r != null
            ? $"N°{r.ReportNumber} — {r.GeneratedAt:dd/MM/yyyy HH:mm}"
            : "Aucun rapport";

    // ══════════════════════════════════════════════
    //  FILTER & SEARCH
    // ══════════════════════════════════════════════

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<ReportListItemViewModel> filtered = SelectedFilter switch
        {
            "Z-Rapport" => _allReports.Where(r => r.Type == ReportType.Z),
            "X-Rapport" => _allReports.Where(r => r.Type == ReportType.X),
            "A-Rapport" => _allReports.Where(r => r.Type == ReportType.A),
            _ => _allReports
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.OperatorName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.ReportNumber.ToString().Contains(q) ||
                r.DateLabel.Contains(q));
        }

        Reports.Clear();
        foreach (var r in filtered)
            Reports.Add(r);

        NoResults = Reports.Count == 0;
    }

    // ══════════════════════════════════════════════
    //  SELECTION & PREVIEW
    // ══════════════════════════════════════════════

    partial void OnSelectedReportChanged(ReportListItemViewModel? value)
    {
        if (value?.PrintContent is not null)
        {
            ReportPreview = value.PrintContent;
            PreviewTitle = value.Title;
            PreviewSubtitle = $"{value.DateLabel}  •  {value.PeriodLabel}  •  {value.OperatorName}";
            ShowPreview = true;
        }
        else
        {
            ShowPreview = false;
            ReportPreview = "";
            PreviewTitle = "";
            PreviewSubtitle = "";
        }
    }

    // ══════════════════════════════════════════════
    //  COMMANDS
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task Refresh()
    {
        ClearStatus();
        SelectedReport = null;
        await LoadAsync();
        ShowStatus("✓ Données actualisées", false);
    }

    [RelayCommand]
    private void CopyReport()
    {
        if (string.IsNullOrEmpty(ReportPreview)) return;
        try
        {
            Clipboard.SetText(ReportPreview);
            ShowStatus("✓ Rapport copié dans le presse-papiers", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur copie : {ex.Message}", true);
        }
    }

    [RelayCommand]
    private void PrintReport()
    {
        if (SelectedReport?.PrintContent is null)
        {
            ShowStatus("Sélectionnez un rapport à imprimer.", true);
            return;
        }

        try
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                PagePadding = new Thickness(40, 30, 40, 30),
                ColumnWidth = double.MaxValue
            };
            doc.Blocks.Add(new Paragraph(new Run(SelectedReport.PrintContent))
            {
                LineHeight = 14
            });

            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(pd.PrintableAreaWidth, pd.PrintableAreaHeight);
            pd.PrintDocument(paginator, PreviewTitle);

            ShowStatus($"✓ {PreviewTitle} envoyé à l'imprimante", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur impression : {ex.Message}", true);
        }
    }

    [RelayCommand]
    private void ExportReport()
    {
        if (SelectedReport?.PrintContent is null)
        {
            ShowStatus("Sélectionnez un rapport à exporter.", true);
            return;
        }

        try
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"{SelectedReport.TypeBadge}-Rapport-{SelectedReport.ReportNumber}_{SelectedReport.GeneratedAt:yyyyMMdd_HHmm}",
                DefaultExt = ".txt",
                Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            File.WriteAllText(dlg.FileName, SelectedReport.PrintContent);
            ShowStatus($"✓ Exporté : {Path.GetFileName(dlg.FileName)}", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur export : {ex.Message}", true);
        }
    }

    // ══════════════════════════════════════════════
    //  STATUS
    // ══════════════════════════════════════════════

    private void ShowStatus(string msg, bool isError)
    {
        StatusMessage = msg;
        ShowSuccess = !isError;
        ShowError = isError;
    }

    private void ClearStatus()
    {
        StatusMessage = "";
        ShowSuccess = false;
        ShowError = false;
    }
}