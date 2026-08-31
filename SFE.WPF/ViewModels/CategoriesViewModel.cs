using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Events;
using SFE.Application.Services;
using SFE.Domain.Entities;

namespace SFE.WPF.ViewModels;

public partial class CategoriesViewModel : BaseViewModel, IActivatable
{
    private readonly CategoryService _categoryService;

    // ══════════════════════════════════════════════
    //  LISTE
    // ══════════════════════════════════════════════
    public ObservableCollection<ProductCategory> Categories { get; } = new();
    private bool _isFirstActivation = true;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _categoryCount;
    [ObservableProperty] private ProductCategory? _selectedCategory;

    // ══════════════════════════════════════════════
    //  FORMULAIRE
    // ══════════════════════════════════════════════
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isNewCategory;
    [ObservableProperty] private string _formTitle = "Nouvelle catégorie";

    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editColor = "#3B82F6";
    [ObservableProperty] private string _editIcon = "📦";
    [ObservableProperty] private string _editSortOrder = "0";
    [ObservableProperty] private bool _editIsActive = true;

    // ══════════════════════════════════════════════
    //  PRESETS
    // ══════════════════════════════════════════════
    public string[] PresetColors { get; } =
    {
        "#3B82F6", "#2563EB", "#0EA5E9", "#06B6D4", "#14B8A6",
        "#10B981", "#22C55E", "#84CC16", "#F59E0B", "#D97706",
        "#F97316", "#EF4444", "#DC2626", "#E11D48", "#EC4899",
        "#A855F7", "#8B5CF6", "#7C3AED", "#6366F1", "#059669"
    };

    public string[] PresetIcons { get; } =
    {
        "📦", "🥤", "🍞", "🧴", "🔧", "📎", "💊", "👕",
        "🏠", "🎮", "📱", "🖥️", "🍕", "🍺", "🧹", "✂️",
        "🎁", "📚", "🛒", "⚡", "🎨", "🌍", "💎", "🔑",
        "🍫", "🧊", "🪥", "🧽", "💡", "🔋", "🎧", "📐"
    };

    // ══════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════
    public CategoriesViewModel(CategoryService categoryService)
    {
        _categoryService = categoryService;
        PageTitle = "Catégories";

        Subscribe(OnCategoryChangedAsync,
            AppEvent.CategoryCreated,
            AppEvent.CategoryUpdated,
            AppEvent.CategoryDeleted);

        _ = InitializeAsync();
    }

    private async Task OnCategoryChangedAsync()
    {
        if (!IsEditing)
        {
            await LoadCategoriesAsync();
        }
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur au chargement : {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ══════════════════════════════════════════════
    //  SEARCH + LOAD
    // ══════════════════════════════════════════════
    private async Task LoadCategoriesAsync()
    {
        var results = await _categoryService.GetAllActiveAsync();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLowerInvariant();
            results = results
                .Where(c => c.Name.ToLowerInvariant().Contains(search))
                .ToList();
        }

        Categories.Clear();
        foreach (var c in results) Categories.Add(c);
        CategoryCount = Categories.Count;
    }

    partial void OnSearchTextChanged(string value) => _ = LoadCategoriesAsync();

    // ══════════════════════════════════════════════
    //  CRUD — NEW
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void StartNewCategory()
    {
        ClearStatus();
        IsNewCategory = true;
        IsEditing = true;
        FormTitle = "Nouvelle catégorie";

        EditId = 0;
        EditName = "";
        EditColor = "#3B82F6";
        EditIcon = "📦";
        EditSortOrder = "0";
        EditIsActive = true;
    }

    // ══════════════════════════════════════════════
    //  CRUD — EDIT
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void StartEditCategory(ProductCategory? category)
    {
        if (category == null) return;

        ClearStatus();
        IsNewCategory = false;
        IsEditing = true;
        FormTitle = $"Modifier « {category.Name} »";
        SelectedCategory = category;

        EditId = category.Id;
        EditName = category.Name;
        EditColor = category.Color;
        EditIcon = category.Icon;
        EditSortOrder = category.SortOrder.ToString();
        EditIsActive = category.IsActive;
    }

    // ══════════════════════════════════════════════
    //  CRUD — SAVE
    // ══════════════════════════════════════════════
    [RelayCommand]
    private async Task SaveCategory()
    {
        ClearStatus();

        if (string.IsNullOrWhiteSpace(EditName))
        {
            ShowErrorMessage("Le nom de la catégorie est obligatoire.");
            return;
        }

        if (!int.TryParse(EditSortOrder, out var sortOrder))
            sortOrder = 0;

        if (IsNewCategory)
        {
            var category = new ProductCategory
            {
                Name = EditName.Trim(),
                Color = EditColor,
                Icon = EditIcon,
                SortOrder = sortOrder,
                IsActive = EditIsActive
            };

            var result = await _categoryService.CreateAsync(category);
            if (!result.Success)
            {
                ShowErrorMessage(result.ErrorMessage);
                return;
            }

            StatusMessage = $"✓ Catégorie « {category.Name} » créée avec succès.";
        }
        else
        {
            var category = await _categoryService.GetByIdAsync(EditId);
            if (category == null)
            {
                ShowErrorMessage("Catégorie introuvable.");
                return;
            }

            category.Name = EditName.Trim();
            category.Color = EditColor;
            category.Icon = EditIcon;
            category.SortOrder = sortOrder;
            category.IsActive = EditIsActive;

            var result = await _categoryService.UpdateAsync(category);
            if (!result.Success)
            {
                ShowErrorMessage(result.ErrorMessage);
                return;
            }

            StatusMessage = $"✓ Catégorie « {category.Name} » mise à jour.";
        }

        ShowSuccess = true;
        IsEditing = false;
        await LoadCategoriesAsync();
    }

    // ══════════════════════════════════════════════
    //  CRUD — CANCEL / DELETE
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ClearStatus();
    }

    [RelayCommand]
    private async Task DeleteCategory(ProductCategory? category)
    {
        if (category == null) return;

        var result = await _categoryService.DeleteAsync(category.Id);
        if (!result.Success)
        {
            ShowErrorMessage(result.ErrorMessage);
            return;
        }

        StatusMessage = $"✓ Catégorie « {category.Name} » supprimée.";
        ShowSuccess = true;
        ShowError = false;
        if (IsEditing && EditId == category.Id) IsEditing = false;
        await LoadCategoriesAsync();
    }

    // ══════════════════════════════════════════════
    //  ICON / COLOR SELECTION
    // ══════════════════════════════════════════════
    [RelayCommand]
    private void SelectColor(string? color)
    {
        if (!string.IsNullOrEmpty(color))
            EditColor = color;
    }

    [RelayCommand]
    private void SelectIcon(string? icon)
    {
        if (!string.IsNullOrEmpty(icon))
            EditIcon = icon;
    }

    // ══════════════════════════════════════════════
    //  IActivatable
    // ══════════════════════════════════════════════
    public async Task ActivateAsync()
    {
        if (_isFirstActivation)
        {
            _isFirstActivation = false;
            return;
        }

        if (IsEditing) return;

        IsBusy = true;
        try
        {
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Erreur au rechargement : {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}