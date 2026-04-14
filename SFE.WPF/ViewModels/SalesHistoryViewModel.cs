using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.WPF.Helpers;

namespace SFE.WPF.ViewModels;

public partial class SalesHistoryViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;

    // ══════ RÉSULTATS ══════
    public ObservableCollection<InvoiceListItemViewModel> Invoices { get; } = new();
    [ObservableProperty] private InvoiceListItemViewModel? _selectedInvoice;

    // 🆕 DOCUMENT VIEW
    [ObservableProperty] private InvoiceDocumentViewModel? _documentViewModel;
    [ObservableProperty] private bool _showDocument;

    // (keep old detail for backward compat if needed)
    [ObservableProperty] private InvoiceDetailViewModel? _invoiceDetail;
    [ObservableProperty] private bool _showDetail;

    // ══════ FILTRES ══════
    [ObservableProperty] private DateTime _dateFrom = DateTime.Today;
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private InvoiceType? _filterType;
    [ObservableProperty] private InvoiceStatus? _filterStatus;
    [ObservableProperty] private PaymentType? _filterPaymentType;
    [ObservableProperty] private string _selectedPeriodPreset = "Aujourd'hui";

    // ══════ PAGINATION ══════
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pageSize = 30;
    [ObservableProperty] private string _paginationInfo = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;

    // ══════ STATS PÉRIODE ══════
    [ObservableProperty] private int _statsTotalCount;
    [ObservableProperty] private decimal _statsTotalTTC;
    [ObservableProperty] private decimal _statsTotalTVA;
    [ObservableProperty] private decimal _statsAverage;
    [ObservableProperty] private int _statsFVCount;
    [ObservableProperty] private int _statsFTCount;
    [ObservableProperty] private int _statsEVCount;

    // ══════ STATUS ══════
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showSuccess;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private bool _noResults;

    // ══════ ENUMS POUR COMBOS ══════
    public InvoiceType?[] FilterTypes { get; } =
    {
        null, InvoiceType.FV, InvoiceType.FT, InvoiceType.EV,
        InvoiceType.ET, InvoiceType.FA, InvoiceType.EA
    };

    public InvoiceStatus?[] FilterStatuses { get; } =
    {
        null, InvoiceStatus.Normalized, InvoiceStatus.Draft,
        InvoiceStatus.Cancelled, InvoiceStatus.Error
    };

    public string[] PeriodPresets { get; } =
    {
        "Aujourd'hui", "Hier", "Cette semaine", "Ce mois",
        "Mois dernier", "Ce trimestre", "Cette année", "Personnalisé"
    };

    public SalesHistoryViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        PageTitle = "Journal des ventes";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        ApplyPeriodPreset("Aujourd'hui");
        await SearchInvoicesAsync();
    }

    // ══════════════════════════════════════════════
    // RECHERCHE & FILTRES (unchanged)
    // ══════════════════════════════════════════════

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
        var today = DateTime.Today;
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
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()
            };

            var (items, totalCount) = await _unitOfWork.Invoices
                .SearchAsync(criteria, CurrentPage, PageSize);

            TotalCount = totalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
            CanGoBack = CurrentPage > 1;
            CanGoForward = CurrentPage < TotalPages;

            int startIndex = (CurrentPage - 1) * PageSize + 1;
            int endIndex = Math.Min(CurrentPage * PageSize, totalCount);
            PaginationInfo = totalCount > 0
                ? $"{startIndex}–{endIndex} sur {totalCount}"
                : "Aucun résultat";

            Invoices.Clear();
            foreach (var invoice in items)
                Invoices.Add(InvoiceListItemViewModel.FromEntity(invoice));

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
        }
        catch { }
    }

    // ══════════════════════════════════════════════
    // PAGINATION (unchanged)
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task GoFirstPage() { CurrentPage = 1; await SearchInvoicesAsync(); }
    [RelayCommand]
    private async Task GoPreviousPage() { if (CurrentPage > 1) { CurrentPage--; await SearchInvoicesAsync(); } }
    [RelayCommand]
    private async Task GoNextPage() { if (CurrentPage < TotalPages) { CurrentPage++; await SearchInvoicesAsync(); } }
    [RelayCommand]
    private async Task GoLastPage() { CurrentPage = TotalPages; await SearchInvoicesAsync(); }

    // ══════════════════════════════════════════════
    // 🆕 VIEW INVOICE DOCUMENT
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task ViewInvoice(InvoiceListItemViewModel? item)
    {
        if (item == null) return;

        IsBusy = true;
        try
        {
            // Load invoice with all details
            var invoice = await _unitOfWork.Invoices.GetWithDetailsAsync(item.InvoiceId);
            if (invoice == null) return;

            // Load company
            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();

            // Load POS if invoice has PointOfSaleId
            PointOfSale? pos = null;
            if (invoice.PointOfSaleId > 0)
            {
                pos = await _unitOfWork.GetRepository<PointOfSale>()
                    .GetByIdAsync(invoice.PointOfSaleId);
            }

            // Get exchange rate from settings or company
            // For now, use a default or load from AppSettings
            decimal exchangeRate = 0;
            try
            {
                var settings = (await _unitOfWork.GetRepository<AppSettings>()
                    .FindAsync(s => true)).FirstOrDefault();
                if (settings != null)
                    exchangeRate = settings.CurrentExchangeRate;
            }
            catch { }

            // Create the document view model
            DocumentViewModel = InvoiceDocumentViewModel.Create(
                invoice, company, pos, exchangeRate);

            ShowDocument = true;
            ShowDetail = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CloseDocument()
    {
        ShowDocument = false;
        DocumentViewModel = null;
    }

    // ══════════════════════════════════════════════
    // 🆕 PRINT INVOICE
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void PrintInvoice()
    {
        if (DocumentViewModel == null) return;

        try
        {
            InvoicePrintHelper.Print(DocumentViewModel);
            StatusMessage = $"✓ Impression lancée — {DocumentViewModel.InvoiceNumber}";
            ShowSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur impression : {ex.Message}";
            ShowError = true;
        }
    }

    // ══════════════════════════════════════════════
    // 🆕 EXPORT PDF / XPS
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void ExportPdf()
    {
        if (DocumentViewModel == null) return;

        try
        {
            InvoicePrintHelper.ExportPdf(DocumentViewModel);
            StatusMessage = $"✓ Export réussi — {DocumentViewModel.InvoiceNumber}";
            ShowSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur export : {ex.Message}";
            ShowError = true;
        }
    }

    // ══════════════════════════════════════════════
    // RÉIMPRESSION (from list - quick print)
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task ReprintInvoice(InvoiceListItemViewModel? item)
    {
        if (item == null) return;

        IsBusy = true;
        try
        {
            var invoice = await _unitOfWork.Invoices.GetWithDetailsAsync(item.InvoiceId);
            if (invoice == null) { StatusMessage = "Facture introuvable."; ShowError = true; return; }

            var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
            var vm = InvoiceDocumentViewModel.Create(invoice, company);

            InvoicePrintHelper.Print(vm);
            StatusMessage = $"✓ Impression lancée — {invoice.InvoiceNumber}";
            ShowSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur impression : {ex.Message}";
            ShowError = true;
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    // COPIER CODE DEF
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void CopyCodeDEF()
    {
        if (DocumentViewModel != null && !string.IsNullOrEmpty(DocumentViewModel.CodeDEFDGI))
        {
            System.Windows.Clipboard.SetText(DocumentViewModel.CodeDEFDGI);
            StatusMessage = "✓ Code DEF copié";
            ShowSuccess = true;
        }
    }

    // ══════════════════════════════════════════════
    // CLOSE OLD DETAIL (kept for compat)
    // ══════════════════════════════════════════════

    [RelayCommand]
    private void CloseDetail()
    {
        ShowDetail = false;
        InvoiceDetail = null;
    }

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
}