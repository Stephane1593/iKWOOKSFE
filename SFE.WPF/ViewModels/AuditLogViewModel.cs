using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class AuditLogViewModel : BaseViewModel, IActivatable
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITimeProvider _time;

    // ═══════════════ RESULTS ═══════════════
    public ObservableCollection<AuditLogItemVm> Entries { get; } = new();
    [ObservableProperty] private AuditLogItemVm? _selectedEntry;
    [ObservableProperty] private bool _showDetailPanel;
    [ObservableProperty] private bool _noResults;

    // ═══════════════ DETAIL PANEL ═══════════════
    [ObservableProperty] private string _detailTimestamp = "";
    [ObservableProperty] private string _detailUser = "";
    [ObservableProperty] private string _detailModule = "";
    [ObservableProperty] private string _detailAction = "";
    [ObservableProperty] private string _detailDescription = "";
    [ObservableProperty] private string _detailEntityType = "";
    [ObservableProperty] private string _detailEntityId = "";
    [ObservableProperty] private string _detailCodeDEF = "";
    [ObservableProperty] private string _detailInvoiceNumber = "";
    [ObservableProperty] private string _detailPointOfSale = "";
    [ObservableProperty] private string _detailJson = "";
    [ObservableProperty] private bool _hasCodeDEF;
    [ObservableProperty] private bool _hasDetails;

    // ═══════════════ FILTERS ═══════════════
    // Kept as DateTime for WPF DatePicker binding; converted to DateTimeOffset at the boundary.
    [ObservableProperty] private DateTime _dateFrom;
    [ObservableProperty] private DateTime _dateTo;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filterUser = "";
    [ObservableProperty] private AuditModule? _filterModule;
    [ObservableProperty] private string _selectedPeriodPreset = "Aujourd'hui";

    // ═══════════════ OPERATORS ═══════════════
    public ObservableCollection<string> AvailableUsers { get; } = new();

    // ═══════════════ PAGINATION ═══════════════
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pageSize = 40;
    [ObservableProperty] private string _paginationInfo = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;

    // ═══════════════ STATS ═══════════════
    [ObservableProperty] private int _statsTotalCount;
    [ObservableProperty] private int _statsInvoiceCount;
    [ObservableProperty] private int _statsReportCount;
    [ObservableProperty] private int _statsAuthCount;
    [ObservableProperty] private int _statsStockCount;
    [ObservableProperty] private int _statsSettingsCount;

    // ═══════════════ COMBOS ═══════════════
    public AuditModule?[] FilterModules { get; } =
    {
        null,
        AuditModule.Invoicing, AuditModule.Reports, AuditModule.Authentication,
        AuditModule.Session, AuditModule.Products, AuditModule.Stock,
        AuditModule.Clients, AuditModule.Users, AuditModule.Settings,
        AuditModule.System
    };

    public string[] PeriodPresets { get; } =
    {
        "Aujourd'hui", "Hier", "Cette semaine", "Ce mois",
        "Mois dernier", "Ce trimestre", "Cette année", "Personnalisé"
    };

    // ═══════════════════════════════════════════
    // CTOR
    // ═══════════════════════════════════════════

    public AuditLogViewModel(IUnitOfWork unitOfWork, ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _time = time;
        PageTitle = "Journal d'audit";

        // Sensible defaults before first activation
        var today = _time.LocalNow.Date;
        _dateFrom = today;
        _dateTo = today;
    }

    public async Task ActivateAsync()
    {
        ApplyPeriodPreset("Aujourd'hui");
        await LoadAsync();
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _unitOfWork.AuditLogs.GetDistinctUserNamesAsync();
            AvailableUsers.Clear();
            AvailableUsers.Add("");
            foreach (var u in users) AvailableUsers.Add(u);
        }
        catch { /* non-blocking */ }
    }

    // ═══════════════════════════════════════════
    // SEARCH
    // ═══════════════════════════════════════════

    [RelayCommand]
    private async Task Search()
    {
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFilters()
    {
        SearchText = "";
        FilterUser = "";
        FilterModule = null;
        SelectedPeriodPreset = "Aujourd'hui";
        ApplyPeriodPreset("Aujourd'hui");
        CurrentPage = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SetPeriod(string preset)
    {
        SelectedPeriodPreset = preset;
        ApplyPeriodPreset(preset);
        CurrentPage = 1;
        await LoadAsync();
    }

    private void ApplyPeriodPreset(string preset)
    {
        var today = _time.LocalNow.Date;
        switch (preset)
        {
            case "Aujourd'hui":
                DateFrom = today; DateTo = today; break;
            case "Hier":
                DateFrom = today.AddDays(-1); DateTo = today.AddDays(-1); break;
            case "Cette semaine":
                var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateFrom = today.AddDays(-diff); DateTo = today; break;
            case "Ce mois":
                DateFrom = new DateTime(today.Year, today.Month, 1); DateTo = today; break;
            case "Mois dernier":
                var lm = today.AddMonths(-1);
                DateFrom = new DateTime(lm.Year, lm.Month, 1);
                DateTo = new DateTime(lm.Year, lm.Month,
                    DateTime.DaysInMonth(lm.Year, lm.Month)); break;
            case "Ce trimestre":
                DateFrom = new DateTime(today.Year,
                    ((today.Month - 1) / 3) * 3 + 1, 1);
                DateTo = today; break;
            case "Cette année":
                DateFrom = new DateTime(today.Year, 1, 1); DateTo = today; break;
        }
    }

    /// <summary>
    /// Converts a local filter DateTime (midnight) into a DateTimeOffset
    /// using the current local offset from the injected time provider.
    /// </summary>
    private DateTimeOffset ToStartOfDayOffset(DateTime date)
    {
        var local = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _time.LocalNow.Offset);
    }

    /// <summary>
    /// Converts a local filter DateTime into a DateTimeOffset representing
    /// the end of that day (23:59:59.9999999).
    /// </summary>
    private DateTimeOffset ToEndOfDayOffset(DateTime date)
    {
        var local = DateTime.SpecifyKind(
            date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _time.LocalNow.Offset);
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        ClearStatus();
        try
        {
            var criteria = new AuditLogSearchCriteria
            {
                DateFrom = ToStartOfDayOffset(DateFrom),
                DateTo = ToEndOfDayOffset(DateTo),
                Module = FilterModule,
                UserName = string.IsNullOrWhiteSpace(FilterUser) ? null : FilterUser.Trim(),
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()
            };

            var (items, totalCount) = await _unitOfWork.AuditLogs
                .SearchAsync(criteria, CurrentPage, PageSize);

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
            CanGoBack = CurrentPage > 1;
            CanGoForward = CurrentPage < TotalPages;

            int start = (CurrentPage - 1) * PageSize + 1;
            int end = Math.Min(CurrentPage * PageSize, totalCount);
            PaginationInfo = totalCount > 0
                ? $"{start}–{end} sur {totalCount}" : "Aucun résultat";

            Entries.Clear();
            foreach (var e in items)
                Entries.Add(AuditLogItemVm.FromEntity(e, _time));

            NoResults = Entries.Count == 0;
            await LoadStatsAsync();
        }
        catch (Exception ex) { ShowErrorMessage($"Erreur : {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var s = await _unitOfWork.AuditLogs.GetStatsAsync(
                ToStartOfDayOffset(DateFrom),
                ToEndOfDayOffset(DateTo));
            StatsTotalCount = s.TotalCount;
            StatsInvoiceCount = s.InvoiceCount;
            StatsReportCount = s.ReportCount;
            StatsAuthCount = s.AuthCount;
            StatsStockCount = s.StockCount;
            StatsSettingsCount = s.SettingsCount;
        }
        catch { /* nice-to-have */ }
    }

    // ═══════════════════════════════════════════
    // PAGINATION
    // ═══════════════════════════════════════════

    [RelayCommand] private async Task GoFirstPage() { CurrentPage = 1; await LoadAsync(); }
    [RelayCommand] private async Task GoPreviousPage() { if (CurrentPage > 1) { CurrentPage--; await LoadAsync(); } }
    [RelayCommand] private async Task GoNextPage() { if (CurrentPage < TotalPages) { CurrentPage++; await LoadAsync(); } }
    [RelayCommand] private async Task GoLastPage() { CurrentPage = TotalPages; await LoadAsync(); }

    // ═══════════════════════════════════════════
    // DETAIL PANEL
    // ═══════════════════════════════════════════

    [RelayCommand]
    private void ViewEntry(AuditLogItemVm? item)
    {
        if (item == null) { ShowDetailPanel = false; return; }
        SelectedEntry = item;

        // Show timestamp in the user's local time zone (via time provider offset)
        var local = item.Timestamp.ToOffset(_time.LocalNow.Offset);
        DetailTimestamp = local.ToString("dd/MM/yyyy HH:mm:ss");
        DetailUser = item.UserName;
        DetailModule = item.ModuleLabel;
        DetailAction = item.ActionLabel;
        DetailDescription = item.Description;
        DetailEntityType = item.EntityType;
        DetailEntityId = item.EntityId;
        DetailCodeDEF = item.CodeDEFDGI;
        DetailInvoiceNumber = item.InvoiceNumber;
        DetailPointOfSale = item.PointOfSaleName;
        DetailJson = FormatJson(item.Details);

        HasCodeDEF = !string.IsNullOrEmpty(item.CodeDEFDGI);
        HasDetails = !string.IsNullOrWhiteSpace(item.Details) && item.Details != "{}";

        ShowDetailPanel = true;
    }

    [RelayCommand] private void CloseDetailPanel() => ShowDetailPanel = false;

    [RelayCommand]
    private void CopyCodeDEF()
    {
        if (!string.IsNullOrEmpty(DetailCodeDEF))
        {
            System.Windows.Clipboard.SetText(DetailCodeDEF);
            _ = ShowSuccessAsync("✓ Code DEF copié");
        }
    }

    // ═══════════════════════════════════════════
    // EXPORT CSV
    // ═══════════════════════════════════════════

    [RelayCommand]
    private async Task ExportCsv()
    {
        IsBusy = true;
        try
        {
            // Fetch ALL matching entries (not just current page)
            var criteria = new AuditLogSearchCriteria
            {
                DateFrom = ToStartOfDayOffset(DateFrom),
                DateTo = ToEndOfDayOffset(DateTo),
                Module = FilterModule,
                UserName = string.IsNullOrWhiteSpace(FilterUser) ? null : FilterUser.Trim(),
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()
            };

            var (items, _) = await _unitOfWork.AuditLogs
                .SearchAsync(criteria, 1, 100_000);

            var dlg = new SaveFileDialog
            {
                FileName = $"AuditLog_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.csv",
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv"
            };

            if (dlg.ShowDialog() != true) return;

            var offset = _time.LocalNow.Offset;
            var sb = new StringBuilder();
            sb.AppendLine("Date;Heure;Utilisateur;Module;Action;Description;" +
                          "N° Facture;Code DEF/DGI;Entité;ID Entité;PDV;Détails");

            foreach (var e in items)
            {
                var localTs = e.Timestamp.ToOffset(offset);
                sb.AppendLine(string.Join(";",
                    Esc(localTs.ToString("dd/MM/yyyy")),
                    Esc(localTs.ToString("HH:mm:ss")),
                    Esc(e.UserName),
                    Esc(e.Module.Label()),
                    Esc(e.Action.Label()),
                    Esc(e.Description),
                    Esc(e.InvoiceNumber),
                    Esc(e.CodeDEFDGI),
                    Esc(e.EntityType),
                    Esc(e.EntityId),
                    Esc(e.PointOfSaleName),
                    Esc(e.Details)));
            }

            await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
            _ = ShowSuccessAsync($"✓ {items.Count} entrées exportées");
        }
        catch (Exception ex) { ShowErrorMessage($"Erreur export : {ex.Message}"); }
        finally { IsBusy = false; }
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    private static string Esc(string v)
        => string.IsNullOrEmpty(v) ? "" : $"\"{v.Replace("\"", "\"\"")}\"";

    private static string FormatJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "{}") return "";
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(raw);
            return System.Text.Json.JsonSerializer.Serialize(
                doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch { return raw; }
    }

    partial void OnDateFromChanged(DateTime value)
    {
        if (SelectedPeriodPreset != "Personnalisé")
            SelectedPeriodPreset = "Personnalisé";
    }

    partial void OnDateToChanged(DateTime value)
    {
        if (SelectedPeriodPreset != "Personnalisé")
            SelectedPeriodPreset = "Personnalisé";
    }
}

// ═══════════════════════════════════════════════════════
// LIST ITEM VIEW MODEL
// ═══════════════════════════════════════════════════════

public class AuditLogItemVm
{
    public long Id { get; set; }

    /// <summary>The original stored timestamp (UTC-relative via DateTimeOffset).</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Timestamp rendered in the user's local time zone (set at creation).</summary>
    public DateTimeOffset LocalTimestamp { get; set; }

    public AuditAction Action { get; set; }
    public AuditModule Module { get; set; }
    public string Description { get; set; } = "";
    public string UserName { get; set; } = "";
    public string CodeDEFDGI { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Details { get; set; } = "";
    public string PointOfSaleName { get; set; } = "";

    // ── Display helpers (in user's local time zone) ──
    public string DateDisplay => LocalTimestamp.ToString("dd/MM/yyyy");
    public string TimeDisplay => LocalTimestamp.ToString("HH:mm:ss");
    public string ModuleLabel => Module.Label();
    public string ActionLabel => Action.Label();
    public string CodeDEFShort => string.IsNullOrEmpty(CodeDEFDGI) ? ""
        : CodeDEFDGI.Length > 24 ? CodeDEFDGI[..24] + "…" : CodeDEFDGI;
    public bool HasCodeDEF => !string.IsNullOrEmpty(CodeDEFDGI);

    // ── Module badge colors ──
    public System.Windows.Media.SolidColorBrush ModuleBadgeBg => new(ModuleBgColor);
    public System.Windows.Media.SolidColorBrush ModuleColor => new(ModuleFgColor);

    private System.Windows.Media.Color ModuleBgColor => Module switch
    {
        AuditModule.Invoicing => System.Windows.Media.Color.FromRgb(0xEF, 0xF6, 0xFF), // blue-50
        AuditModule.Reports => System.Windows.Media.Color.FromRgb(0xED, 0xE9, 0xFE), // violet-50
        AuditModule.Authentication => System.Windows.Media.Color.FromRgb(0xEC, 0xFD, 0xF5), // emerald-50
        AuditModule.Session => System.Windows.Media.Color.FromRgb(0xEC, 0xFD, 0xF5),
        AuditModule.Products => System.Windows.Media.Color.FromRgb(0xFE, 0xF3, 0xC7), // amber-100
        AuditModule.Stock => System.Windows.Media.Color.FromRgb(0xFE, 0xF3, 0xC7),
        AuditModule.Clients => System.Windows.Media.Color.FromRgb(0xF0, 0xF9, 0xFF), // sky-50
        AuditModule.Users => System.Windows.Media.Color.FromRgb(0xFC, 0xE7, 0xF3), // pink-100
        AuditModule.Settings => System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9), // slate-100
        AuditModule.System => System.Windows.Media.Color.FromRgb(0xFE, 0xE2, 0xE2), // red-100
        _ => System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9)
    };

    private System.Windows.Media.Color ModuleFgColor => Module switch
    {
        AuditModule.Invoicing => System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6),
        AuditModule.Reports => System.Windows.Media.Color.FromRgb(0x8B, 0x5C, 0xF6),
        AuditModule.Authentication => System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69),
        AuditModule.Session => System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69),
        AuditModule.Products => System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06),
        AuditModule.Stock => System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06),
        AuditModule.Clients => System.Windows.Media.Color.FromRgb(0x03, 0x84, 0xC7),
        AuditModule.Users => System.Windows.Media.Color.FromRgb(0xDB, 0x27, 0x77),
        AuditModule.Settings => System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69),
        AuditModule.System => System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44),
        _ => System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69)
    };

    // ── Action icon ──
    public string ActionIcon => Action switch
    {
        AuditAction.UserLogin or AuditAction.UserLogout => "●",
        AuditAction.InvoiceNormalized or AuditAction.CreditNoteNormalized
            or AuditAction.AdvanceInvoiceNormalized => "✓",
        AuditAction.ReportZGenerated or AuditAction.ReportXGenerated
            or AuditAction.ReportAGenerated => "◆",
        _ when (int)Action >= 500 && (int)Action < 600 => "◇",
        _ when (int)Action >= 600 && (int)Action < 700 => "▸",
        _ => "·"
    };

    public static AuditLogItemVm FromEntity(AuditLogEntry e, ITimeProvider time) => new()
    {
        Id = e.Id,
        Timestamp = e.Timestamp,
        LocalTimestamp = e.Timestamp.ToOffset(time.LocalNow.Offset),
        Action = e.Action,
        Module = e.Module,
        Description = e.Description,
        UserName = e.UserName,
        CodeDEFDGI = e.CodeDEFDGI,
        InvoiceNumber = e.InvoiceNumber,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        Details = e.Details,
        PointOfSaleName = e.PointOfSaleName
    };
}