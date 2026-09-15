using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.WPF.ViewModels;

public partial class RestaurantConfigViewModel : BaseViewModel, IActivatable
{
    private readonly ITableService _tableService;

    // ═══════ NAVIGATION ET ONGLETS ═══════
    [ObservableProperty] private bool _isTablesTab = true;
    [ObservableProperty] private bool _isCategoriesTab = false;
    [ObservableProperty] private bool _isMenusTab = false;

    // ═══════ TABLES LIST ═══════
    public ObservableCollection<Table> Tables { get; } = new();
    [ObservableProperty] private int _tableCount;

    // ═══════ TABLE FORM ═══════
    [ObservableProperty] private bool _isEditingTable;
    [ObservableProperty] private bool _isCreatingTable;
    [ObservableProperty] private string _tableFormTitle = "";
    private int _editingTableId;

    [ObservableProperty] private int _formTableNumber;
    [ObservableProperty] private int _formTableSeats;
    [ObservableProperty] private bool _formIsActive = true;

    // ═══════ CATEGORIES FORM & LIST ═══════
    [ObservableProperty] private bool _isEditingCategory;
    [ObservableProperty] private bool _isCreatingCategory;
    [ObservableProperty] private string _categoryFormTitle = "";
    private int _editingCategoryId;
    [ObservableProperty] private string _formCategoryName = "";
    [ObservableProperty] private PrinterProfile? _formCategoryPrinter;

    // ═══════ MENUS & ROUTAGE ═══════
    public ObservableCollection<Menu> Menus { get; } = new();
    [ObservableProperty] private Menu? _selectedMenu;

    public ObservableCollection<MenuItem> MenuItems { get; } = new();
    [ObservableProperty] private int _menuItemCount;

    public ObservableCollection<Product> AvailableProducts { get; } = new();
    public ObservableCollection<PrinterProfile> AvailablePrinters { get; } = new();

    [ObservableProperty] private bool _isEditingMenuItem;
    [ObservableProperty] private string _menuItemFormTitle = "";
    private int _editingMenuItemId;

    [ObservableProperty] private string _formMenuName = "";
    [ObservableProperty] private decimal _formUnitPrice;
    [ObservableProperty] private Product? _formLinkedProduct;
    [ObservableProperty] private PrinterProfile? _formPrinterProfile;

    public RestaurantConfigViewModel(ITableService tableService)
    {
        _tableService = tableService ?? throw new ArgumentNullException(nameof(tableService));
        PageTitle = "Configuration Restaurant";
    }

    public async Task ActivateAsync()
    {
        await LoadTablesAsync();
        await LoadMenusDataAsync();
    }

    // ═══════ COMMANDES NAVIGATION ═══════
    [RelayCommand]
    private async Task SwitchToTablesTab()
    {
        IsTablesTab = true;
        IsCategoriesTab = false;
        IsMenusTab = false;
        CancelMenuItemEdit();
        CancelCategoryEdit();
        await LoadTablesAsync();
    }

    [RelayCommand]
    private async Task SwitchToCategoriesTab()
    {
        IsTablesTab = false;
        IsCategoriesTab = true;
        IsMenusTab = false;
        CancelTableEdit();
        CancelMenuItemEdit();
        await LoadMenusDataAsync();
    }

    [RelayCommand]
    private async Task SwitchToMenusTab()
    {
        IsTablesTab = false;
        IsCategoriesTab = false;
        IsMenusTab = true;
        CancelTableEdit();
        CancelCategoryEdit();
        await LoadMenusDataAsync();
    }

    // ═══════ CHARGEMENT TABLES ═══════
    private async Task LoadTablesAsync()
    {
        try
        {
            IsBusy = true;
            var tables = await _tableService.GetAllAsync();

            Tables.Clear();
            foreach (var t in tables.OrderBy(x => x.Number))
            {
                Tables.Add(t);
            }
            TableCount = Tables.Count;
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur : {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewTable()
    {
        ClearTableForm();
        IsCreatingTable = true;
        IsEditingTable = true;
        TableFormTitle = "Nouvelle Table";
        FormTableNumber = Tables.Any() ? Tables.Max(t => t.Number) + 1 : 1;
        FormTableSeats = 4;
    }

    [RelayCommand]
    private void EditTable(Table? table)
    {
        if (table == null) return;
        _editingTableId = table.Id;
        IsCreatingTable = false;
        IsEditingTable = true;
        TableFormTitle = $"Modifier — Table {table.Number}";
        FormTableNumber = table.Number;
        FormTableSeats = table.Seats;
        FormIsActive = true;
        ClearStatus();
    }

    [RelayCommand]
    private async Task SaveTable()
    {
        ClearStatus();
        if (FormTableNumber <= 0 || FormTableSeats <= 0)
        {
            ShowErrorMessage("Le numéro de table et le nombre de couverts doivent être supérieurs à 0.");
            return;
        }

        try
        {
            IsBusy = true;
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tableRepo = uow.GetRepository<Table>();

            var currentCompany = await uow.Companies.GetCurrentCompanyAsync();
            int currentCompanyId = currentCompany?.Id ?? 1;

            if (IsCreatingTable)
            {
                if (Tables.Any(t => t.Number == FormTableNumber))
                {
                    ShowErrorMessage($"La table numéro {FormTableNumber} existe déjà.");
                    return;
                }
                await tableRepo.AddAsync(new Table
                {
                    RestaurantId = 1,
                    CompanyId = currentCompanyId,
                    Number = FormTableNumber,
                    Seats = FormTableSeats,
                    Status = TableStatus.Free
                });
            }
            else
            {
                var existingTable = (await tableRepo.FindAsync(t => t.Id == _editingTableId)).FirstOrDefault();
                if (existingTable != null)
                {
                    existingTable.Number = FormTableNumber;
                    existingTable.Seats = FormTableSeats;
                    await tableRepo.UpdateAsync(existingTable);
                }
            }

            await uow.SaveChangesAsync();
            IsEditingTable = false;
            await LoadTablesAsync();
            _ = ShowSuccessAsync(IsCreatingTable ? "✓ Table créée avec succès." : "✓ Table mise à jour.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteTable(Table? table)
    {
        if (table == null) return;
        ClearStatus();

        if (table.Status != TableStatus.Free)
        {
            ShowErrorMessage("Impossible de supprimer une table actuellement occupée ou en nettoyage.");
            return;
        }

        try
        {
            IsBusy = true;
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tableRepo = uow.GetRepository<Table>();

            var existingTable = (await tableRepo.FindAsync(t => t.Id == table.Id)).FirstOrDefault();
            if (existingTable != null)
            {
                await tableRepo.DeleteAsync(existingTable);
                await uow.SaveChangesAsync();
            }

            if (IsEditingTable && _editingTableId == table.Id) CancelTableEdit();
            await LoadTablesAsync();
            _ = ShowSuccessAsync("✓ Table supprimée.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur lors de la suppression : {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelTableEdit()
    {
        IsEditingTable = false;
        IsCreatingTable = false;
        ClearTableForm();
        ClearStatus();
    }

    private void ClearTableForm()
    {
        _editingTableId = 0;
        FormTableNumber = 0;
        FormTableSeats = 0;
        TableFormTitle = "";
    }

    // ═══════ GESTION CATÉGORIES ═══════
    [RelayCommand]
    private void NewCategory()
    {
        _editingCategoryId = 0;
        FormCategoryName = "";
        FormCategoryPrinter = null; 
        CategoryFormTitle = "Nouvelle Catégorie";
        IsCreatingCategory = true;
        IsEditingCategory = true;
        ClearStatus();
    }

    [RelayCommand]
    private void EditCategory(Menu? menu)
    {
        if (menu == null) return;
        _editingCategoryId = menu.Id;
        FormCategoryName = menu.Name;
        FormCategoryPrinter = AvailablePrinters.FirstOrDefault(p => p.Id == menu.PrinterProfileId);
        CategoryFormTitle = $"Modifier — {menu.Name}";
        IsCreatingCategory = false;
        IsEditingCategory = true;
        ClearStatus();
    }

    [RelayCommand]
    private async Task SaveCategory()
    {
        ClearStatus();
        if (string.IsNullOrWhiteSpace(FormCategoryName))
        {
            ShowErrorMessage("Le nom de la catégorie est obligatoire.");
            return;
        }

        try
        {
            IsBusy = true;
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var menuRepo = uow.GetRepository<Menu>();

            var currentCompany = await uow.Companies.GetCurrentCompanyAsync();
            int currentCompanyId = currentCompany?.Id ?? 1;

            if (IsCreatingCategory)
            {
                await menuRepo.AddAsync(new Menu
                {
                    RestaurantId = 1,
                    CompanyId = currentCompanyId,
                    Name = FormCategoryName.Trim(),
                    PrinterProfileId = FormCategoryPrinter?.Id // 🆕 NEW
                });
            }
            else
            {
                var existing = (await menuRepo.FindAsync(m => m.Id == _editingCategoryId)).FirstOrDefault();
                if (existing != null)
                {
                    existing.Name = FormCategoryName.Trim();
                    existing.PrinterProfileId = FormCategoryPrinter?.Id; // 🆕 NEW
                    await menuRepo.UpdateAsync(existing);
                }
            }

            await uow.SaveChangesAsync();
            IsEditingCategory = false;
            await LoadMenusDataAsync();
            _ = ShowSuccessAsync("✓ Catégorie enregistrée.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCategory(Menu? menu)
    {
        if (menu == null) return;
        ClearStatus();

        try
        {
            IsBusy = true;
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var menuRepo = uow.GetRepository<Menu>();

            var existing = (await menuRepo.FindAsync(m => m.Id == menu.Id)).FirstOrDefault();
            if (existing != null)
            {
                await menuRepo.DeleteAsync(existing);
                await uow.SaveChangesAsync();
            }

            if (IsEditingCategory && _editingCategoryId == menu.Id) CancelCategoryEdit();
            await LoadMenusDataAsync();
            _ = ShowSuccessAsync("✓ Catégorie supprimée.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur : {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelCategoryEdit()
    {
        IsEditingCategory = false;
        IsCreatingCategory = false;
        FormCategoryName = "";
        ClearStatus();
    }

    // ═══════ CHARGEMENT MENUS & ROUTAGE ═══════
    private async Task LoadMenusDataAsync()
    {
        try
        {
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var products = await uow.Products.GetAllAsync();
            AvailableProducts.Clear();
            foreach (var p in products.Where(x => x.IsActive).OrderBy(x => x.Name))
                AvailableProducts.Add(p);

            var printers = await uow.GetRepository<PrinterProfile>().GetAllAsync();
            AvailablePrinters.Clear();
            foreach (var pr in printers)
                AvailablePrinters.Add(pr);

            var menus = await uow.GetRepository<Menu>().GetAllAsync();
            Menus.Clear();
            foreach (var m in menus)
                Menus.Add(m);

            if (Menus.Any() && SelectedMenu == null)
                SelectedMenu = Menus.First();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur de chargement : {ex.Message}");
        }
    }

    partial void OnSelectedMenuChanged(Menu? value)
    {
        _ = LoadMenuItemsAsync(value?.Id);
    }

    private async Task LoadMenuItemsAsync(int? menuId)
    {
        if (menuId == null)
        {
            MenuItems.Clear();
            MenuItemCount = 0;
            return;
        }

        try
        {
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var items = await uow.GetRepository<MenuItem>().FindAsync(m => m.MenuId == menuId.Value);

            MenuItems.Clear();
            foreach (var item in items.OrderBy(x => x.Name))
                MenuItems.Add(item);

            MenuItemCount = MenuItems.Count;
        }
        catch { }
    }

    [RelayCommand]
    private void NewMenuItem()
    {
        if (SelectedMenu == null)
        {
            ShowErrorMessage("Veuillez d'abord sélectionner une catégorie de menu.");
            return;
        }

        _editingMenuItemId = 0;
        FormMenuName = "";
        FormUnitPrice = 0;
        FormLinkedProduct = null;
        FormPrinterProfile = null;
        MenuItemFormTitle = "Nouvel article au menu";
        IsEditingMenuItem = true;
        ClearStatus();
    }

    [RelayCommand]
    private void EditMenuItem(MenuItem? item)
    {
        if (item == null) return;

        _editingMenuItemId = item.Id;
        FormMenuName = item.Name;
        FormUnitPrice = item.UnitPrice;
        FormLinkedProduct = AvailableProducts.FirstOrDefault(p => p.Id == item.ProductId);
        FormPrinterProfile = AvailablePrinters.FirstOrDefault(p => p.Id == item.PrinterProfileId);
        MenuItemFormTitle = $"Modifier — {item.Name}";
        IsEditingMenuItem = true;
        ClearStatus();
    }

    [RelayCommand]
    private async Task SaveMenuItem()
    {
        if (SelectedMenu == null || FormLinkedProduct == null)
        {
            ShowErrorMessage("Le produit lié fiscal est obligatoire.");
            return;
        }

        try
        {
            IsBusy = true;
            var uow = App.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var repo = uow.GetRepository<MenuItem>();

            var currentCompany = await uow.Companies.GetCurrentCompanyAsync();
            int currentCompanyId = currentCompany?.Id ?? 1;

            if (_editingMenuItemId == 0)
            {
                var newItem = new MenuItem
                {
                    MenuId = SelectedMenu.Id,
                    ProductId = FormLinkedProduct.Id,
                    CompanyId = currentCompanyId,
                    Name = string.IsNullOrWhiteSpace(FormMenuName) ? FormLinkedProduct.Name : FormMenuName,
                    UnitPrice = FormUnitPrice,
                    PrinterProfileId = FormPrinterProfile?.Id,
                    IsAvailable = true
                };
                await repo.AddAsync(newItem);
            }
            else
            {
                var existing = (await repo.FindAsync(m => m.Id == _editingMenuItemId)).FirstOrDefault();
                if (existing != null)
                {
                    existing.ProductId = FormLinkedProduct.Id;
                    existing.Name = string.IsNullOrWhiteSpace(FormMenuName) ? FormLinkedProduct.Name : FormMenuName;
                    existing.UnitPrice = FormUnitPrice;
                    existing.PrinterProfileId = FormPrinterProfile?.Id;
                    await repo.UpdateAsync(existing);
                }
            }

            await uow.SaveChangesAsync();
            IsEditingMenuItem = false;
            await LoadMenuItemsAsync(SelectedMenu.Id);
            _ = ShowSuccessAsync("✓ Article sauvegardé.");
        }
        catch (Exception ex)
        {
            ShowErrorMessage(ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelMenuItemEdit()
    {
        IsEditingMenuItem = false;
        ClearStatus();
    }

    partial void OnFormLinkedProductChanged(Product? value)
    {
        if (value != null)
        {
            if (_editingMenuItemId == 0)
            {
                FormMenuName = value.Name;
                FormUnitPrice = value.UnitPriceTtcCdf;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(FormMenuName))
                    FormMenuName = value.Name;

                if (FormUnitPrice == 0)
                    FormUnitPrice = value.UnitPriceTtcCdf;
            }
        }
    }
}