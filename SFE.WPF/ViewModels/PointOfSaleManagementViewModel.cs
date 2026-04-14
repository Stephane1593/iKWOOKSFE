using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System.Collections.ObjectModel;

namespace SFE.WPF.ViewModels;

public partial class PointOfSaleManagementViewModel : BaseViewModel
{
    private readonly PointOfSaleService _posService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockService _stockService;

    public PointOfSaleManagementViewModel(
        PointOfSaleService posService, IUnitOfWork unitOfWork, StockService stockService)
    {
        _posService = posService;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        PageTitle = "Points de vente";
    }

    // ══════════════════════════════════════════════
    //  PROPRIÉTÉS
    // ══════════════════════════════════════════════

    [ObservableProperty]
    private int _companyId;

    [ObservableProperty]
    private ObservableCollection<PointOfSale> _allPos = new();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formTitle = "";

    // ── Champs formulaire ──

    private int _editId;

    [ObservableProperty]
    private string _editCode = "";

    [ObservableProperty]
    private string _editName = "";

    [ObservableProperty]
    private string _editAddress = "";

    [ObservableProperty]
    private string _editCity = "";

    [ObservableProperty]
    private string _editPhone = "";

    [ObservableProperty]
    private bool _editManagesStock = true;

    [ObservableProperty]
    private bool _editAllowNegativeStock;

    [ObservableProperty]
    private string _editEmcfUrl = "";

    [ObservableProperty]
    private string _editEmcfToken = "";

    [ObservableProperty]
    private string _editEmcfNim = "";

    // ── Radio fiscal (bool au lieu de l'enum directement) ──

    private bool _editIsEmcf = true;
    public bool EditIsEmcf
    {
        get => _editIsEmcf;
        set
        {
            if (SetProperty(ref _editIsEmcf, value))
                OnPropertyChanged(nameof(EditIsEmcf));
        }
    }

    // ══════════════════════════════════════════════
    //  COMMANDES
    // ══════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;   // ← simplified

        var posList = await _posService.GetAllAsync(CompanyId);
        AllPos = new ObservableCollection<PointOfSale>(posList);
    }

    [RelayCommand]
    private async Task StartNewPosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;

        _editId = 0;
        EditCode = await _posService.GenerateNextCodeAsync(CompanyId);
        EditName = "";
        EditAddress = "";
        EditCity = "";
        EditPhone = "";
        EditManagesStock = true;
        EditAllowNegativeStock = false;
        EditIsEmcf = true;                // ← synchronisé
        EditEmcfUrl = "";
        EditEmcfToken = "";
        EditEmcfNim = "";
        FormTitle = "Nouveau point de vente";
        IsEditing = true;
    }

    [RelayCommand]
    private void EditPos(PointOfSale pos)
    {
        _editId = pos.Id;
        EditCode = pos.Code;
        EditName = pos.Name;
        EditAddress = pos.Address;
        EditCity = pos.City;
        EditPhone = pos.Phone;
        EditManagesStock = pos.ManagesStock;
        EditAllowNegativeStock = pos.AllowNegativeStock;
        EditIsEmcf = pos.DeviceType == DeviceType.EMcf;  // ← synchronisé
        EditEmcfUrl = pos.EmcfApiUrl ?? "";
        EditEmcfToken = pos.EmcfToken ?? "";
        EditEmcfNim = pos.EmcfNIM ?? "";
        FormTitle = $"Modifier {pos.Code}";
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SavePosAsync()
    {
        if (!await EnsureCompanyLoadedAsync()) return;
        // ✅ Conversion bool → enum AVANT la sauvegarde
        var deviceType = EditIsEmcf ? DeviceType.EMcf : DeviceType.Mcf;

        PosSaveResult result;

        if (_editId == 0)
        {
            var pos = new PointOfSale
            {
                CompanyId = CompanyId,
                Code = EditCode,
                Name = EditName,
                Address = EditAddress,
                City = EditCity,
                Phone = EditPhone,
                ManagesStock = EditManagesStock,
                AllowNegativeStock = EditAllowNegativeStock,
                DeviceType = deviceType,                        // ← corrigé
                EmcfApiUrl = NullIfEmpty(EditEmcfUrl),
                EmcfToken = NullIfEmpty(EditEmcfToken),
                EmcfNIM = NullIfEmpty(EditEmcfNim)
            };
            result = await _posService.CreateAsync(pos);
        }
        else
        {
            var pos = await _posService.GetByIdAsync(_editId);
            if (pos == null) { ShowErrorMessage("POS introuvable."); return; }

            pos.Code = EditCode;
            pos.Name = EditName;
            pos.Address = EditAddress;
            pos.City = EditCity;
            pos.Phone = EditPhone;
            pos.ManagesStock = EditManagesStock;
            pos.AllowNegativeStock = EditAllowNegativeStock;
            pos.DeviceType = deviceType;                        // ← corrigé
            pos.EmcfApiUrl = NullIfEmpty(EditEmcfUrl);
            pos.EmcfToken = NullIfEmpty(EditEmcfToken);
            pos.EmcfNIM = NullIfEmpty(EditEmcfNim);

            result = await _posService.UpdateAsync(pos);
        }

        if (result.Success)
        {
            IsEditing = false;
            await LoadAsync();
            _ = ShowSuccessAsync(_editId == 0 ? "✅ POS créé avec succès." : "✅ POS mis à jour.");
        }
        else
        {
            ShowErrorMessage(result.ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task DeactivatePosAsync(PointOfSale pos)
    {
        var result = await _posService.DeactivateAsync(pos.Id);
        if (result.Success)
        {
            await LoadAsync();
            _ = ShowSuccessAsync($"POS {pos.Code} désactivé.");
        }
        else ShowErrorMessage(result.ErrorMessage);
    }

    [RelayCommand]
    private async Task InitializeStockAsync(PointOfSale pos)
    {
        IsBusy = true;
        try
        {
            var count = await _stockService.InitializePosStockFromProductsAsync(pos.Id, "Admin");
            _ = ShowSuccessAsync($"✅ {count} produit(s) initialisé(s) dans {pos.Code}.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur initialisation stock : {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════

    private async Task<bool> EnsureCompanyLoadedAsync()
    {
        if (CompanyId > 0) return true;

        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company != null)
        {
            CompanyId = company.Id;
            return true;
        }

        ShowErrorMessage("Aucune entreprise configurée.");
        return false;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

}