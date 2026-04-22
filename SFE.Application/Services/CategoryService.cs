using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Application.Services;

public class CategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ProductCategory>> GetAllActiveAsync()
    {
        return await _unitOfWork.ProductCategories.GetActiveCategoriesAsync();
    }

    public async Task<ProductCategory?> GetByIdAsync(int id)
    {
        return await _unitOfWork.ProductCategories.GetByIdAsync(id);
    }

    public async Task<ProductCategory?> GetWithProductsAsync(int id)
    {
        return await _unitOfWork.ProductCategories.GetWithProductsAsync(id);
    }

    public async Task<CategorySaveResult> CreateAsync(ProductCategory category)
    {
        var validation = Validate(category);
        if (!validation.IsValid)
            return new CategorySaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        // Check name uniqueness among active categories
        var existing = await _unitOfWork.ProductCategories.GetActiveCategoriesAsync();
        if (existing.Any(c => c.Name.Equals(category.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return new CategorySaveResult
            {
                Success = false,
                ErrorMessage = $"Une catégorie « {category.Name.Trim()} » existe déjà."
            };

        category.CreatedAt = DateTime.Now;
        await _unitOfWork.ProductCategories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        _unitOfWork.EnqueueEvent(AppEvent.CategoryCreated, category.Id.ToString());
        await _unitOfWork.FlushEventsAsync();

        return new CategorySaveResult { Success = true, CategoryId = category.Id };
    }

    public async Task<CategorySaveResult> UpdateAsync(ProductCategory category)
    {
        var validation = Validate(category);
        if (!validation.IsValid)
            return new CategorySaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        // Check name uniqueness (exclude self)
        var existing = await _unitOfWork.ProductCategories.GetActiveCategoriesAsync();
        if (existing.Any(c => c.Id != category.Id
                           && c.Name.Equals(category.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return new CategorySaveResult
            {
                Success = false,
                ErrorMessage = $"Une catégorie « {category.Name.Trim()} » existe déjà."
            };

        await _unitOfWork.ProductCategories.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();

        _unitOfWork.EnqueueEvent(AppEvent.CategoryUpdated, category.Id.ToString());
        await _unitOfWork.FlushEventsAsync();

        return new CategorySaveResult { Success = true, CategoryId = category.Id };
    }

    public async Task<CategorySaveResult> DeleteAsync(int categoryId)
    {
        var category = await _unitOfWork.ProductCategories.GetWithProductsAsync(categoryId);
        if (category == null)
            return new CategorySaveResult { Success = false, ErrorMessage = "Catégorie introuvable." };

        if (category.Products.Count > 0)
            return new CategorySaveResult
            {
                Success = false,
                ErrorMessage = $"Impossible de supprimer : cette catégorie contient "
                             + $"{category.Products.Count} produit(s) actif(s). "
                             + "Réaffectez-les d'abord."
            };

        category.IsActive = false;
        await _unitOfWork.ProductCategories.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();

        _unitOfWork.EnqueueEvent(AppEvent.CategoryDeleted, categoryId.ToString());
        await _unitOfWork.FlushEventsAsync();

        return new CategorySaveResult { Success = true, CategoryId = categoryId };
    }

    private static ValidationResult Validate(ProductCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            return new ValidationResult("Le nom de la catégorie est obligatoire.");

        if (category.Name.Trim().Length > 100)
            return new ValidationResult("Le nom ne peut pas dépasser 100 caractères.");

        if (string.IsNullOrWhiteSpace(category.Color))
            return new ValidationResult("La couleur est obligatoire.");

        return new ValidationResult { IsValid = true };
    }
}

public class CategorySaveResult
{
    public bool Success { get; set; }
    public int CategoryId { get; set; }
    public string ErrorMessage { get; set; } = "";
}