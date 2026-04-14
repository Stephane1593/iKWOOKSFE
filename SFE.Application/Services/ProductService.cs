// File: SFE.Application/Services/ProductService.cs
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class ProductService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Product>> GetAllActiveAsync()
    {
        return await _unitOfWork.Products.GetActiveProductsAsync();
    }

    public async Task<List<Product>> SearchAsync(string query, int maxResults = 20)
    {
        return await _unitOfWork.Products.SearchAsync(query, maxResults);
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _unitOfWork.Products.GetByIdAsync(id);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        return await _unitOfWork.Products.GetByBarcodeAsync(barcode);
    }

    public async Task<ProductSaveResult> CreateAsync(Product product)
    {
        var validation = Validate(product);
        if (!validation.IsValid)
            return new ProductSaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        if (!string.IsNullOrWhiteSpace(product.Code))
        {
            var existing = await _unitOfWork.Products.GetByCodeAsync(product.Code);
            if (existing != null)
                return new ProductSaveResult { Success = false, ErrorMessage = $"Le code « {product.Code} » est déjà utilisé." };
        }

        product.CreatedAt = DateTime.Now;
        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // ── EVENT ──
        _unitOfWork.EnqueueEvent(AppEvent.ProductCreated, product.Id.ToString());
        await _unitOfWork.FlushEventsAsync();

        return new ProductSaveResult { Success = true, ProductId = product.Id };
    }

    public async Task<ProductSaveResult> UpdateAsync(Product product)
    {
        var validation = Validate(product);
        if (!validation.IsValid)
            return new ProductSaveResult { Success = false, ErrorMessage = validation.ErrorMessage };

        if (!string.IsNullOrWhiteSpace(product.Code))
        {
            var existing = await _unitOfWork.Products.GetByCodeAsync(product.Code);
            if (existing != null && existing.Id != product.Id)
                return new ProductSaveResult { Success = false, ErrorMessage = $"Le code « {product.Code} » est déjà utilisé." };
        }

        product.UpdatedAt = DateTime.Now;
        await _unitOfWork.Products.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // ── EVENT ──
        _unitOfWork.EnqueueEvent(AppEvent.ProductUpdated, product.Id.ToString());
        await _unitOfWork.FlushEventsAsync();

        return new ProductSaveResult { Success = true, ProductId = product.Id };
    }

    public async Task DeleteAsync(int productId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product != null)
        {
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;
            await _unitOfWork.Products.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync();

            // ── EVENT ──
            _unitOfWork.EnqueueEvent(AppEvent.ProductDeleted, productId.ToString());
            await _unitOfWork.FlushEventsAsync();
        }
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync()
    {
        return await _unitOfWork.ProductCategories.GetActiveCategoriesAsync();
    }

    public async Task<ProductCategory> CreateCategoryAsync(string name, string color = "#3B82F6", string icon = "📦")
    {
        var category = new ProductCategory
        {
            Name = name,
            Color = color,
            Icon = icon
        };
        await _unitOfWork.ProductCategories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return category;
    }

    public async Task SeedSampleDataAsync()
    {
        var existingCount = await _unitOfWork.Products.GetActiveCountAsync();
        if (existingCount > 0) return;

        var catBoissons = new ProductCategory { Name = "Boissons", Color = "#3B82F6", Icon = "🥤", SortOrder = 1 };
        var catAliments = new ProductCategory { Name = "Alimentation", Color = "#10B981", Icon = "🍞", SortOrder = 2 };
        var catHygiene = new ProductCategory { Name = "Hygiène", Color = "#8B5CF6", Icon = "🧴", SortOrder = 3 };
        var catServices = new ProductCategory { Name = "Services", Color = "#F59E0B", Icon = "🔧", SortOrder = 4 };
        var catBureau = new ProductCategory { Name = "Fournitures bureau", Color = "#EF4444", Icon = "📎", SortOrder = 5 };

        await _unitOfWork.ProductCategories.AddAsync(catBoissons);
        await _unitOfWork.ProductCategories.AddAsync(catAliments);
        await _unitOfWork.ProductCategories.AddAsync(catHygiene);
        await _unitOfWork.ProductCategories.AddAsync(catServices);
        await _unitOfWork.ProductCategories.AddAsync(catBureau);
        await _unitOfWork.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { Code = "BRS-001", Name = "Eau minérale 1.5L", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 2500, Unit = "btle", CategoryId = catBoissons.Id, StockQuantity = 200, MinStockLevel = 20, TrackStock = true, IsFavorite = true },
            new() { Code = "BRS-002", Name = "Coca-Cola 33cl", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 3000, Unit = "btle", CategoryId = catBoissons.Id, StockQuantity = 150, MinStockLevel = 15, TrackStock = true, IsFavorite = true },
            new() { Code = "BRS-003", Name = "Jus d'orange 1L", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 5500, Unit = "btle", CategoryId = catBoissons.Id, StockQuantity = 80, MinStockLevel = 10, TrackStock = true },
            new() { Code = "BRS-004", Name = "Bière Primus 65cl", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 3500, Unit = "btle", CategoryId = catBoissons.Id, StockQuantity = 300, MinStockLevel = 30, TrackStock = true, IsFavorite = true },
            new() { Code = "BRS-005", Name = "Eau de Javel 1L", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 4000, Unit = "btle", CategoryId = catHygiene.Id, StockQuantity = 50, MinStockLevel = 5, TrackStock = true },
            new() { Code = "ALM-001", Name = "Pain blanc 400g", ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
                     UnitPrice = 1500, Unit = "pce", CategoryId = catAliments.Id, StockQuantity = 50, MinStockLevel = 10, TrackStock = true, IsFavorite = true },
            new() { Code = "ALM-002", Name = "Riz local 5kg", ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
                     UnitPrice = 12000, Unit = "sac", CategoryId = catAliments.Id, StockQuantity = 100, MinStockLevel = 10, TrackStock = true },
            new() { Code = "ALM-003", Name = "Huile de palme 1L", ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
                     UnitPrice = 8000, Unit = "btle", CategoryId = catAliments.Id, StockQuantity = 60, MinStockLevel = 8, TrackStock = true },
            new() { Code = "ALM-004", Name = "Sucre 1kg", ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
                     UnitPrice = 5000, Unit = "pqt", CategoryId = catAliments.Id, StockQuantity = 80, MinStockLevel = 10, TrackStock = true },
            new() { Code = "ALM-005", Name = "Lait en poudre 400g", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 9500, Unit = "bte", CategoryId = catAliments.Id, StockQuantity = 40, MinStockLevel = 5, TrackStock = true },
            new() { Code = "HYG-001", Name = "Savon de toilette", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 2000, Unit = "pce", CategoryId = catHygiene.Id, StockQuantity = 100, MinStockLevel = 10, TrackStock = true },
            new() { Code = "HYG-002", Name = "Papier hygiénique x4", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 6000, Unit = "pqt", CategoryId = catHygiene.Id, StockQuantity = 70, MinStockLevel = 10, TrackStock = true },
            new() { Code = "SRV-001", Name = "Consultation technique", ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
                     UnitPrice = 50000, Unit = "h", CategoryId = catServices.Id },
            new() { Code = "SRV-002", Name = "Installation logiciel", ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
                     UnitPrice = 75000, Unit = "fft", CategoryId = catServices.Id },
            new() { Code = "SRV-003", Name = "Maintenance mensuelle", ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
                     UnitPrice = 150000, Unit = "mois", CategoryId = catServices.Id },
            new() { Code = "BUR-001", Name = "Ramette papier A4", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 15000, Unit = "ram", CategoryId = catBureau.Id, StockQuantity = 30, MinStockLevel = 5, TrackStock = true },
            new() { Code = "BUR-002", Name = "Stylo bille bleu", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 500, Unit = "pce", CategoryId = catBureau.Id, StockQuantity = 200, MinStockLevel = 20, TrackStock = true },
            new() { Code = "BUR-003", Name = "Cartouche d'encre HP", ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
                     UnitPrice = 45000, Unit = "pce", CategoryId = catBureau.Id, StockQuantity = 10, MinStockLevel = 3, TrackStock = true },
        };

        foreach (var p in products)
            await _unitOfWork.Products.AddAsync(p);

        // No events for seed data — one-time setup
        await _unitOfWork.SaveChangesAsync();
    }

    private static ValidationResult Validate(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return new ValidationResult("Le nom du produit est obligatoire.");

        if (product.UnitPrice < 0)
            return new ValidationResult("Le prix unitaire ne peut pas être négatif.");

        if (product.TrackStock && product.StockQuantity < 0)
            return new ValidationResult("Le stock ne peut pas être négatif.");

        if ((product.TaxGroup == TaxGroup.L || product.TaxGroup == TaxGroup.N) && product.ItemType != ItemType.TAX)
            return new ValidationResult("Les groupes L et N nécessitent le type d'article TAX.");

        return new ValidationResult { IsValid = true };
    }
}

public class ProductSaveResult
{
    public bool Success { get; set; }
    public int ProductId { get; set; }
    public string ErrorMessage { get; set; } = "";
}