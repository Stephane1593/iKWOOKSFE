using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class ReportViewModel : BaseViewModel
{
    private readonly ReportService _reportService;
    private readonly IUnitOfWork _unitOfWork;

    // ══════ LIST ══════
    public ObservableCollection<ReportListItemViewModel> Reports { get; } = new();

    [ObservableProperty] private ReportListItemViewModel? _selectedReport;

    // ══════ PREVIEW ══════
    [ObservableProperty] private string _reportPreview = "";
    [ObservableProperty] private bool _showPreview;
    [ObservableProperty] private string _previewTitle = "";
    [ObservableProperty] private string _previewSubtitle = "";

    // ══════ PERIODIC X ══════
    [ObservableProperty] private DateTime _periodicFrom = DateTime.Today;
    [ObservableProperty] private DateTime _periodicTo = DateTime.Today;

    // ══════ FILTER ══════
    [ObservableProperty] private string _selectedFilter = "Tous";
    public string[] FilterOptions { get; } =
        { "Tous", "Z-Rapport", "X-Rapport", "A-Rapport" };

    // ══════ STATS ══════
    [ObservableProperty] private string _lastZInfo = "Aucun";
    [ObservableProperty] private string _lastXInfo = "Aucun";
    [ObservableProperty] private string _lastAInfo = "Aucun";
    [ObservableProperty] private int _zCount;
    [ObservableProperty] private int _xCount;
    [ObservableProperty] private int _aCount;

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _noResults;

    // ══════ CONFIRMATION ══════
    [ObservableProperty] private bool _showConfirmation;
    [ObservableProperty] private string _confirmTitle = "";
    [ObservableProperty] private string _confirmMessage = "";
    [ObservableProperty] private string _confirmButtonText = "Confirmer";
    private Func<Task>? _pendingAction;

    // TODO: inject from authentication / session service
    private string OperatorName => "Opérateur";

    public ReportViewModel(IUnitOfWork unitOfWork, ReportService reportService)
    {
        _unitOfWork = unitOfWork;
        _reportService = reportService;
        PageTitle = "Rapports fiscaux";

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadReportsAsync();
        await LoadStatsAsync();
    }

    // ══════════════════════════════════════════════
    // DATA LOADING
    // ══════════════════════════════════════════════

    private async Task LoadReportsAsync()
    {
        IsBusy = true;
        try
        {
            ReportType? typeFilter = SelectedFilter switch
            {
                "Z-Rapport" => ReportType.Z,
                "X-Rapport" => ReportType.X,
                "A-Rapport" => ReportType.A,
                _ => null
            };

            var all = await _unitOfWork.GetRepository<DailyReport>()
                .FindAsync(r => typeFilter == null || r.Type == typeFilter);

            var sorted = all
                .OrderByDescending(r => r.GeneratedAt)
                .Take(200)
                .ToList();

            Reports.Clear();
            foreach (var r in sorted)
                Reports.Add(ReportListItemViewModel.FromEntity(r));

            NoResults = Reports.Count == 0;
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur chargement : {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var all = (await _unitOfWork.GetRepository<DailyReport>()
                .FindAsync(r => true)).ToList();

            var lastZ = all.Where(r => r.Type == ReportType.Z)
                .OrderByDescending(r => r.GeneratedAt).FirstOrDefault();
            var lastX = all.Where(r => r.Type == ReportType.X)
                .OrderByDescending(r => r.GeneratedAt).FirstOrDefault();
            var lastA = all.Where(r => r.Type == ReportType.A)
                .OrderByDescending(r => r.GeneratedAt).FirstOrDefault();

            ZCount = all.Count(r => r.Type == ReportType.Z);
            XCount = all.Count(r => r.Type == ReportType.X);
            ACount = all.Count(r => r.Type == ReportType.A);

            LastZInfo = lastZ != null
                ? $"N°{lastZ.ReportNumber} — {lastZ.GeneratedAt:dd/MM/yyyy HH:mm}"
                : "Aucun";
            LastXInfo = lastX != null
                ? $"N°{lastX.ReportNumber} — {lastX.GeneratedAt:dd/MM/yyyy HH:mm}"
                : "Aucun";
            LastAInfo = lastA != null
                ? $"N°{lastA.ReportNumber} — {lastA.GeneratedAt:dd/MM/yyyy HH:mm}"
                : "Aucun";
        }
        catch { /* stats are non-critical */ }
    }

    // ══════════════════════════════════════════════
    // GENERATE COMMANDS
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateXDaily()
    {
        await RunGenerationAsync("X-Rapport quotidien", async () =>
            await _reportService.GenerateReportXAsync(OperatorName));
    }

    [RelayCommand]
    private async Task GenerateXPeriodic()
    {
        if (PeriodicFrom > PeriodicTo)
        {
            ShowStatus("La date de début doit être antérieure à la date de fin.", true);
            return;
        }

        var end = PeriodicTo.Date.AddDays(1).AddSeconds(-1);
        await RunGenerationAsync("X-Rapport périodique", async () =>
            await _reportService.GenerateReportXPeriodicAsync(OperatorName, PeriodicFrom, end));
    }

    [RelayCommand]
    private void RequestGenerateZ()
    {
        ConfirmTitle = "⚠ Clôture — Z-Rapport";
        ConfirmMessage =
            "Le Z-Rapport effectue la clôture de la session.\n\n" +
            "Cette action :\n" +
            "  • Agrège toutes les transactions depuis le dernier Z\n" +
            "  • Marque la fin de la période comptable\n" +
            "  • Démarre une nouvelle session\n\n" +
            "Voulez-vous continuer ?";
        ConfirmButtonText = "Générer Z-Rapport";
        _pendingAction = async () =>
            await RunGenerationAsync("Z-Rapport", async () =>
                await _reportService.GenerateReportZAsync(OperatorName));
        ShowConfirmation = true;
    }

    [RelayCommand]
    private async Task GenerateA()
    {
        await RunGenerationAsync("A-Rapport", async () =>
            await _reportService.GenerateReportAAsync(OperatorName));
    }

    private async Task RunGenerationAsync(string label, Func<Task<DailyReport>> generate)
    {
        IsBusy = true;
        ClearStatus();

        try
        {
            var report = await generate();

            await LoadReportsAsync();
            await LoadStatsAsync();

            // Auto-select newly created report
            var item = Reports.FirstOrDefault(r => r.ReportId == report.Id);
            if (item != null)
            {
                SelectedReport = item;
                ShowPreviewFor(item);
            }

            ShowStatus($"✓ {label} N°{report.ReportNumber} généré avec succès", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur {label} : {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ══════════════════════════════════════════════
    // CONFIRMATION
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task ConfirmAction()
    {
        ShowConfirmation = false;
        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null;
            await action();
        }
    }

    [RelayCommand]
    private void CancelConfirmation()
    {
        ShowConfirmation = false;
        _pendingAction = null;
    }

    // ══════════════════════════════════════════════
    // SELECTION & PREVIEW
    // ══════════════════════════════════════════════

    partial void OnSelectedReportChanged(ReportListItemViewModel? value)
    {
        if (value != null)
            ShowPreviewFor(value);
    }

    private void ShowPreviewFor(ReportListItemViewModel item)
    {
        ReportPreview = item.PrintContent ?? "(Contenu non disponible)";
        PreviewTitle = item.Title;
        PreviewSubtitle = $"{item.DateLabel}  •  {item.PeriodLabel}  •  {item.InvoiceCountLabel}";
        ShowPreview = true;
    }

    [RelayCommand]
    private void ClosePreview()
    {
        ShowPreview = false;
        ReportPreview = "";
        PreviewTitle = "";
        PreviewSubtitle = "";
        SelectedReport = null;
    }

    // ══════════════════════════════════════════════
    // PRINT / COPY
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void PrintReport()
    {
        if (string.IsNullOrEmpty(ReportPreview)) return;

        try
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                PagePadding = new Thickness(40, 30, 40, 30),
                ColumnWidth = double.MaxValue
            };

            doc.Blocks.Add(new Paragraph(new Run(ReportPreview))
            {
                LineHeight = 14
            });

            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
            dlg.PrintDocument(paginator, PreviewTitle);

            ShowStatus($"✓ {PreviewTitle} envoyé à l'imprimante", false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Erreur impression : {ex.Message}", true);
        }
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

    // ══════════════════════════════════════════════
    // FILTER
    // ══════════════════════════════════════════════

    partial void OnSelectedFilterChanged(string value)
    {
        _ = LoadReportsAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        ClearStatus();
        await LoadReportsAsync();
        await LoadStatsAsync();
        ShowStatus("✓ Données actualisées", false);
    }

    // ══════════════════════════════════════════════
    // STATUS
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