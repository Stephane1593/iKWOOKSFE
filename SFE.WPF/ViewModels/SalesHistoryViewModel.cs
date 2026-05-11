using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SFE.Application.Services;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace SFE.WPF.ViewModels;

public partial class SalesHistoryViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;
    private readonly IAuthService _auth;
    private readonly ITimeProvider _timeProvider;   // 🆕

    // ══════════════════════ RESULTS ══════════════════════
    public ObservableCollection<InvoiceListItemViewModel> Invoices { get; } = new();
    [ObservableProperty] private InvoiceListItemViewModel? _selectedInvoice;

    // ══════════════════════ DOCUMENT / DETAIL PANEL ══════
    [ObservableProperty] private InvoiceDocumentViewModel? _documentViewModel;
    [ObservableProperty] private bool _showDocument;
    [ObservableProperty] private bool _showDetailPanel;

    // Detail-panel header
    [ObservableProperty] private string _detailTypeLabel = "";
    [ObservableProperty] private string _detailNumber = "";
    [ObservableProperty] private string _detailDate = "";
    [ObservableProperty] private string _detailOperator = "";
    [ObservableProperty] private string _detailClient = "";
    [ObservableProperty] private string _detailClientNIF = "";
    [ObservableProperty] private string _detailCodeDEF = "";

    // Detail totals
    [ObservableProperty] private string _detailTotalHT = "";
    [ObservableProperty] private string _detailTotalTVA = "";
    [ObservableProperty] private string _detailTotalTTC = "";

    // Detail items
    public ObservableCollection<InvoiceDetailLineVm> DetailLines { get; } = new();

    // Legacy detail
    [ObservableProperty] private InvoiceDetailViewModel? _invoiceDetail;
    [ObservableProperty] private bool _showDetail;

    // ══════════════════════ OPERATORS ═══════════════════
    public ObservableCollection<string> AvailableOperators { get; } = new();

    // ══════════════════════ FILTERS ══════════════════════
    [ObservableProperty] private DateTime _dateFrom = DateTime.Today;
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filterOperator = "";
    [ObservableProperty] private string _filterReference = "";
    [ObservableProperty] private InvoiceType? _filterType;
    [ObservableProperty] private InvoiceStatus? _filterStatus;
    [ObservableProperty] private PaymentType? _filterPaymentType;
    [ObservableProperty] private string _selectedPeriodPreset = "Aujourd'hui";

    // ══════════════════════ PAGINATION ═══════════════════
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pageSize = 30;
    [ObservableProperty] private string _paginationInfo = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;

    // ══════════════════════ STATS ════════════════════════
    [ObservableProperty] private int _statsTotalCount;
    [ObservableProperty] private decimal _statsTotalTTC;
    [ObservableProperty] private decimal _statsTotalTVA;
    [ObservableProperty] private decimal _statsAverage;
    [ObservableProperty] private int _statsFVCount;
    [ObservableProperty] private int _statsFTCount;
    [ObservableProperty] private int _statsEVCount;
    [ObservableProperty] private int _statsFACount;
    [ObservableProperty] private int _statsEACount;
    [ObservableProperty] private int _statsETCount;

    // ══════════════════════ STATUS ═══════════════════════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _noResults;

    // ══════════════════════ PRINT STATUS (detail panel) ══════
    [ObservableProperty] private string _detailPrintStatus = "";
    [ObservableProperty] private string _detailPrintBadge = "";
    [ObservableProperty] private Brush _detailPrintBadgeFg = Brushes.Gray;
    [ObservableProperty] private Brush _detailPrintBadgeBg = Brushes.Transparent;

    // ══════════════════════ COMBOS ═══════════════════════
    public InvoiceType?[] FilterTypes { get; } =
    {
        null,
        InvoiceType.FV, InvoiceType.FA, InvoiceType.FT,
        InvoiceType.EV, InvoiceType.EA, InvoiceType.ET
    };

    public InvoiceStatus?[] FilterStatuses { get; } =
    {
        null,
        InvoiceStatus.Normalized, InvoiceStatus.Draft,
        InvoiceStatus.Cancelled, InvoiceStatus.Error
    };

    public string[] PeriodPresets { get; } =
    {
        "Aujourd'hui", "Hier", "Cette semaine", "Ce mois",
        "Mois dernier", "Ce trimestre", "Cette année", "Personnalisé"
    };

    // ═════════════════════════════════════════════════════
    //  CTOR
    // ═════════════════════════════════════════════════════

    public SalesHistoryViewModel(
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService,
        IAuthService auth,
        ITimeProvider timeProvider)               // 🆕
    {
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
        _auth = auth;
        _timeProvider = timeProvider;             // 🆕
        PageTitle = "Journal des ventes";
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task ConvertProforma(InvoiceListItemViewModel? item)
    {
        if (item == null || !item.IsConvertibleProforma) return;

        var dlg = new SFE.WPF.Views.Pages.ConvertProformaDialog(item.InvoiceNumber, item.TotalTTC)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        ClearStatus();

        try
        {
            var result = await _invoiceService.ConvertProformaAsync(
                proformaId: item.InvoiceId,
                targetType: dlg.SelectedType,
                operatorName: _auth.CurrentUser?.FullName ?? "—",
                operatorId: _auth.CurrentUser?.Id.ToString() ?? "01",
                advanceAmount: dlg.AdvanceAmount);

            if (result.Success)
            {
                StatusMessage = $"✓ Proforma convertie en {dlg.SelectedType} — Code DEF: {result.CodeDEFDGI}";
                ShowSuccess = true;
                await SearchInvoicesAsync();
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Échec de la conversion.";
                ShowError = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    private async Task InitializeAsync()
    {
        ApplyPeriodPreset("Aujourd'hui");
        await SearchInvoicesAsync();
        await LoadOperatorsAsync();
    }

    private async Task LoadOperatorsAsync()
    {
        try
        {
            var operators = await _unitOfWork.Invoices.GetDistinctOperatorNamesAsync();

            AvailableOperators.Clear();
            AvailableOperators.Add("");

            foreach (var name in operators.OrderBy(n => n))
                AvailableOperators.Add(name);
        }
        catch { /* non-blocking */ }
    }

    // ═════════════════════════════════════════════════════
    //  SEARCH & FILTERS
    // ═════════════════════════════════════════════════════

    [RelayCommand]
    private async Task Search()
    {
        CurrentPage = 1;
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private async Task ResetFilters()
    {
        SearchText = "";
        FilterOperator = "";
        FilterReference = "";
        FilterType = null;
        FilterStatus = null;
        FilterPaymentType = null;
        SelectedPeriodPreset = "Aujourd'hui";
        ApplyPeriodPreset("Aujourd'hui");
        CurrentPage = 1;
        await SearchInvoicesAsync();
    }

    [RelayCommand]
    private async Task SetPeriod(string preset)
    {
        SelectedPeriodPreset = preset;
        ApplyPeriodPreset(preset);
        CurrentPage = 1;
        await SearchInvoicesAsync();
    }

    private void ApplyPeriodPreset(string preset)
    {
        // 🆕 Use local "today" from the time provider rather than DateTime.Today
        var today = _timeProvider.LocalNow.Date;
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
                DateTo = new DateTime(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month)); break;
            case "Ce trimestre":
                DateFrom = new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1);
                DateTo = today; break;
            case "Cette année":
                DateFrom = new DateTime(today.Year, 1, 1); DateTo = today; break;
        }
    }

    private async Task SearchInvoicesAsync()
    {
        IsBusy = true;
        ClearStatus();

        try
        {
            var criteria = new InvoiceSearchCriteria
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                Type = FilterType,
                Status = FilterStatus,
                PaymentType = FilterPaymentType,
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                OperatorName = string.IsNullOrWhiteSpace(FilterOperator) ? null : FilterOperator.Trim(),
                Reference = string.IsNullOrWhiteSpace(FilterReference) ? null : FilterReference.Trim()
            };

            var (items, totalCount) = await _unitOfWork.Invoices
                .SearchAsync(criteria, CurrentPage, PageSize);

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
            CanGoBack = CurrentPage > 1;
            CanGoForward = CurrentPage < TotalPages;

            int start = (CurrentPage - 1) * PageSize + 1;
            int end = Math.Min(CurrentPage * PageSize, totalCount);
            PaginationInfo = totalCount > 0
                ? $"{start}–{end} sur {totalCount}"
                : "Aucun résultat";

            Invoices.Clear();
            foreach (var inv in items)
                Invoices.Add(InvoiceListItemViewModel.FromEntity(inv, _timeProvider)); // 🆕

            NoResults = Invoices.Count == 0;
            await LoadPeriodStatsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    private async Task LoadPeriodStatsAsync()
    {
        try
        {
            var stats = await _unitOfWork.Invoices.GetPeriodStatsAsync(DateFrom, DateTo);
            StatsTotalCount = stats.TotalCount;
            StatsTotalTTC = stats.TotalTTC;
            StatsTotalTVA = stats.TotalTVA;
            StatsAverage = stats.AverageAmount;
            StatsFVCount = stats.FVCount;
            StatsFTCount = stats.FTCount;
            StatsEVCount = stats.EVCount;
            StatsFACount = stats.FACount;
            StatsEACount = stats.EACount;
            StatsETCount = stats.ETCount;
        }
        catch { /* stats are nice-to-have */ }
    }

    // ═════════════════════════════════════════════════════
    //  PAGINATION
    // ═════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GoFirstPage() { CurrentPage = 1; await SearchInvoicesAsync(); }
    [RelayCommand]
    private async Task GoPreviousPage() { if (CurrentPage > 1) { CurrentPage--; await SearchInvoicesAsync(); } }
    [RelayCommand]
    private async Task GoNextPage() { if (CurrentPage < TotalPages) { CurrentPage++; await SearchInvoicesAsync(); } }
    [RelayCommand]
    private async Task GoLastPage() { CurrentPage = TotalPages; await SearchInvoicesAsync(); }

    // ═════════════════════════════════════════════════════
    //  VIEW INVOICE → INLINE DETAIL PANEL
    // ═════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ViewInvoice(InvoiceListItemViewModel? item)
    {
        if (item == null) return;
        IsBusy = true;
        ClearStatus();

        try
        {
            var invoice = await _unitOfWork.Invoices.GetWithDetailsAsync(item.InvoiceId);
            if (invoice == null) return;

            // Preview only — doesn't burn a tirage number
            DocumentViewModel = await BuildDocumentViewModelAsync(invoice, printNumber: null);

            // 🆕 Local projection for all DateTimeOffset fields
            var createdLocal = _timeProvider.ToLocal(invoice.CreatedAt);
            var firstPrintLocal = invoice.FirstPrintedAt is { } f ? _timeProvider.ToLocal(f) : (DateTimeOffset?)null;
            var lastPrintLocal = invoice.LastPrintedAt is { } l ? _timeProvider.ToLocal(l) : (DateTimeOffset?)null;

            // Populate detail panel header
            DetailTypeLabel = invoice.Type.Label();
            DetailNumber = invoice.InvoiceNumber ?? $"#{invoice.Id}";
            DetailDate = createdLocal.ToString("dd/MM/yyyy HH:mm");   // 🆕
            DetailOperator = invoice.OperatorName ?? "—";
            DetailClient = invoice.ClientName ?? "Client comptoir";
            DetailClientNIF = invoice.ClientNIF ?? "—";
            DetailCodeDEF = invoice.CodeDEFDGI ?? "—";

            // Print status
            DetailPrintBadge = invoice.PrintCount switch
            {
                0 => "JAMAIS IMPRIMÉE",
                1 => "ORIGINAL",
                _ => $"DUPLICATA ×{invoice.PrintCount - 1}"
            };

            DetailPrintStatus = invoice.PrintCount switch
            {
                0 => "Aucun tirage enregistré",
                1 => $"Original imprimé le {firstPrintLocal:dd/MM/yyyy 'à' HH:mm}",   // 🆕
                _ => $"{invoice.PrintCount} tirages · dernier {lastPrintLocal:dd/MM/yyyy 'à' HH:mm}" // 🆕
            };

            DetailPrintBadgeFg = invoice.PrintCount switch
            {
                0 => new SolidColorBrush(MediaColor.FromRgb(0x94, 0xA3, 0xB8)),
                1 => new SolidColorBrush(MediaColor.FromRgb(0x05, 0x96, 0x69)),
                _ => new SolidColorBrush(MediaColor.FromRgb(0xC6, 0x28, 0x28))
            };

            DetailPrintBadgeBg = invoice.PrintCount switch
            {
                0 => new SolidColorBrush(MediaColor.FromArgb(0x1A, 0x94, 0xA3, 0xB8)),
                1 => new SolidColorBrush(MediaColor.FromArgb(0x1A, 0x05, 0x96, 0x69)),
                _ => new SolidColorBrush(MediaColor.FromArgb(0x1A, 0xC6, 0x28, 0x28))
            };

            DetailTotalHT = invoice.TotalHT.ToString("N0") + " CDF";
            DetailTotalTVA = invoice.TotalTVA.ToString("N0") + " CDF";
            DetailTotalTTC = invoice.TotalTTC.ToString("N0") + " CDF";

            DetailLines.Clear();
            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    DetailLines.Add(new InvoiceDetailLineVm
                    {
                        Description = line.Name ?? "—",
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        TotalHT = line.AmountHT,
                        TVARate = line.TaxRate
                    });
                }
            }

            ShowDetailPanel = true;
            ShowDocument = false;
            SelectedInvoice = item;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    // ═════════════════════════════════════════════════════
    //  DETAIL PANEL ACTIONS
    // ═════════════════════════════════════════════════════

    [RelayCommand]
    private void CloseDetailPanel() => ShowDetailPanel = false;

    [RelayCommand]
    private void PopOutDetail()
    {
        if (DocumentViewModel == null) return;
        ShowDetailPanel = false;
        ShowDocument = true;
    }

    [RelayCommand]
    private void CloseDocument() => ShowDocument = false;

    // ═════════════════════════════════════════════════════
    //  PRINT / EXPORT / COPY
    // ═════════════════════════════════════════════════════

    private async Task<InvoiceDocumentViewModel?> BuildDocumentViewModelAsync(
        Invoice invoice, int? printNumber = null)
    {
        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();

        PointOfSale? pos = null;
        if (invoice.PointOfSaleId > 0)
            pos = await _unitOfWork.GetRepository<PointOfSale>().GetByIdAsync(invoice.PointOfSaleId);

        decimal exchangeRate = 0;
        try
        {
            var settings = (await _unitOfWork.GetRepository<AppSettings>()
                .FindAsync(s => true)).FirstOrDefault();
            if (settings != null) exchangeRate = settings.CurrentExchangeRate;
        }
        catch { /* non-blocking */ }

        byte[]? logoBytes = null;
        try
        {
            if (company != null && company.Logo != null && company.Logo.Length > 0)
                logoBytes = company.Logo;
        }
        catch { /* non-blocking */ }

        // 🆕 Pass the ITimeProvider (correct signature: invoice, company, timeProvider, pos, rate)
        var vm = InvoiceDocumentViewModel.Create(invoice, company, _timeProvider, pos, exchangeRate);

        if (vm != null)
        {
            vm.SourceInvoice = invoice;
            vm.SourceCompany = company;
            vm.SourcePos = pos;
            vm.SourceExchangeRate = exchangeRate;
            vm.SourceLogoBytes = logoBytes;

            // Print marker — peek (no DB write) by default
            vm.PrintNumber = printNumber ?? Math.Max(1, invoice.PrintCount + 1);
        }

        return vm;
    }

    [RelayCommand]
    private async Task PrintInvoice()
    {
        if (DocumentViewModel?.SourceInvoice == null) return;
        ClearStatus();
        IsBusy = true;

        try
        {
            var time = _timeProvider;
            var vm = await RegisterPrintAndBuildVmAsync(DocumentViewModel.SourceInvoice.Id);
            if (vm == null)
            {
                StatusMessage = "Impossible de préparer le document.";
                ShowError = true;
                return;
            }

            DocumentViewModel = vm;

            InvoicePrintHelper.Print(vm, time);

            var marker = vm.PrintNumber <= 1 ? "Original" : $"Duplicata N°{vm.PrintNumber - 1}";
            StatusMessage = $"✓ {marker} — {vm.InvoiceNumber}";
            ShowSuccess = true;

            await RefreshSelectedRowAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur impression : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (DocumentViewModel?.SourceInvoice == null) return;
        ClearStatus();
        IsBusy = true;

        try
        {
            var invoiceId = DocumentViewModel.SourceInvoice.Id;
            var time = _timeProvider;

            int peekNo = await _invoiceService.PeekPrintNumberAsync(invoiceId);
            var inv = await _unitOfWork.Invoices.GetWithDetailsAsync(invoiceId);
            if (inv == null) { StatusMessage = "Facture introuvable."; ShowError = true; return; }

            var vm = await BuildDocumentViewModelAsync(inv, peekNo);
            if (vm == null) { StatusMessage = "Impossible de préparer le document."; ShowError = true; return; }

            if (InvoicePrintHelper.ExportPdf(vm,time))
            {
                try { await _invoiceService.RegisterPrintAsync(invoiceId); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Export] RegisterPrint failed: {ex.Message}"); }

                DocumentViewModel = vm;
                var marker = vm.PrintNumber <= 1 ? "Original" : $"Duplicata N°{vm.PrintNumber - 1}";
                StatusMessage = $"✓ Export réussi ({marker}) — {vm.InvoiceNumber}";
                ShowSuccess = true;

                await RefreshSelectedRowAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur export : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReprintInvoice(InvoiceListItemViewModel? item)
    {
        if (item == null) return;
        IsBusy = true;
        ClearStatus();

        try
        {
            var time = _timeProvider;
            var vm = await RegisterPrintAndBuildVmAsync(item.InvoiceId);
            if (vm == null)
            {
                StatusMessage = "Facture introuvable ou impossible à charger.";
                ShowError = true;
                return;
            }

            InvoicePrintHelper.Print(vm, time);

            var marker = vm.PrintNumber <= 1 ? "Original" : $"Duplicata N°{vm.PrintNumber - 1}";
            StatusMessage = $"✓ {marker} — {vm.InvoiceNumber}";
            ShowSuccess = true;

            await RefreshRowAsync(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur impression : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CopyCodeDEF()
    {
        if (DocumentViewModel != null && !string.IsNullOrEmpty(DocumentViewModel.CodeDEFDGI))
        {
            Clipboard.SetText(DocumentViewModel.CodeDEFDGI);
            StatusMessage = "✓ Code DEF copié";
            ShowSuccess = true;
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        ShowDetail = false;
        InvoiceDetail = null;
    }

    // ═════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════

    private void ClearStatus()
    {
        StatusMessage = "";
        ShowSuccess = false;
        ShowError = false;
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

    private async Task<InvoiceDocumentViewModel?> RegisterPrintAndBuildVmAsync(int invoiceId)
    {
        int printNo;
        try
        {
            printNo = await _invoiceService.RegisterPrintAsync(invoiceId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Print] RegisterPrint failed: {ex.Message}");
            printNo = 1;
        }

        var invoice = await _unitOfWork.Invoices.GetWithDetailsAsync(invoiceId);
        if (invoice == null) return null;

        return await BuildDocumentViewModelAsync(invoice, printNo);
    }

    /// <summary>Refreshes a specific row in the grid (PrintCount, dates, status).</summary>
    private async Task RefreshRowAsync(InvoiceListItemViewModel item)
    {
        try
        {
            var fresh = await _unitOfWork.Invoices.GetByIdAsync(item.InvoiceId);
            if (fresh == null) return;

            // 🆕 Convert DateTimeOffset? → DateTime? via the time provider
            item.PrintCount = fresh.PrintCount;
            item.FirstPrintedAt = fresh.FirstPrintedAt is { } f
                ? _timeProvider.ToLocal(f).DateTime
                : (DateTime?)null;
            item.LastPrintedAt = fresh.LastPrintedAt is { } l
                ? _timeProvider.ToLocal(l).DateTime
                : (DateTime?)null;
        }
        catch { /* non-blocking */ }
    }

    private async Task RefreshSelectedRowAsync()
    {
        if (SelectedInvoice != null)
            await RefreshRowAsync(SelectedInvoice);
    }
}

// ═════════════════════════════════════════════════════
//  LINE ITEM VM for the inline detail panel
// ═════════════════════════════════════════════════════

public class InvoiceDetailLineVm
{
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TVARate { get; set; }

    public string QuantityDisplay => Quantity.ToString("N0");
    public string UnitPriceDisplay => UnitPrice.ToString("N0");
    public string TotalDisplay => TotalHT.ToString("N0");
    public string TVADisplay => TVARate > 0 ? $"{TVARate}%" : "0%";
}