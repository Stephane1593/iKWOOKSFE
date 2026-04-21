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
    // ══════════════════════════════════════════
    //  🥤 BOISSONS
    // ══════════════════════════════════════════
    new()
    {
        Code = "BRS-001", Barcode = "6901234567001", Name = "Eau minérale 1.5L",
        Description = "Eau minérale naturelle, bouteille plastique 1.5 litres",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 2500, Unit = "btle",
        UnitPriceHtCdf = 2500, UnitPriceTtcCdf = 2500, UnitPriceHtUsd = 0.91m, UnitPriceTtcUsd = 0.91m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 200, MinStockLevel = 20, TrackStock = true,
        IsActive = true, IsFavorite = true, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BRS-002", Barcode = "5449000000996", Name = "Coca-Cola 33cl",
        Description = "Boisson gazeuse Coca-Cola, canette 33 cl",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 3000, Unit = "btle",
        UnitPriceHtCdf = 2586, UnitPriceTtcCdf = 3000, UnitPriceHtUsd = 0.94m, UnitPriceTtcUsd = 1.09m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 150, MinStockLevel = 15, TrackStock = true,
        IsActive = true, IsFavorite = true, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BRS-003", Barcode = "6901234567003", Name = "Jus d'orange 1L",
        Description = "Jus d'orange 100% pur jus, brique 1 litre",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 5500, Unit = "btle",
        UnitPriceHtCdf = 4741, UnitPriceTtcCdf = 5500, UnitPriceHtUsd = 1.72m, UnitPriceTtcUsd = 2.00m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 80, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BRS-004", Barcode = "6901234567004", Name = "Bière Primus 65cl",
        Description = "Bière blonde Primus, bouteille 65 cl",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.C,
        SpecificTaxType = SpecificTaxType.Percentage, SpecificTaxValue = 10, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 3500, Unit = "btle",
        UnitPriceHtCdf = 3017, UnitPriceTtcCdf = 3500, UnitPriceHtUsd = 1.10m, UnitPriceTtcUsd = 1.27m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 300, MinStockLevel = 30, TrackStock = true,
        IsActive = true, IsFavorite = true, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BRS-005", Barcode = "6901234567005", Name = "Whisky local 75cl",
        Description = "Whisky de fabrication locale, bouteille 75 cl",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.C,
        SpecificTaxType = SpecificTaxType.Percentage, SpecificTaxValue = 10, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 18000, Unit = "btle",
        UnitPriceHtCdf = 15517, UnitPriceTtcCdf = 18000, UnitPriceHtUsd = 5.64m, UnitPriceTtcUsd = 6.55m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 40, MinStockLevel = 5, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BRS-006", Barcode = "6901234567006", Name = "Vin rouge importé 75cl",
        Description = "Vin rouge d'importation, bouteille 75 cl",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.C,
        SpecificTaxType = SpecificTaxType.Percentage, SpecificTaxValue = 10, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 25000, Unit = "btle",
        UnitPriceHtCdf = 21552, UnitPriceTtcCdf = 25000, UnitPriceHtUsd = 7.84m, UnitPriceTtcUsd = 9.09m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBoissons.Id,
        StockQuantity = 25, MinStockLevel = 3, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },

    // ══════════════════════════════════════════
    //  🍞 ALIMENTS
    // ══════════════════════════════════════════
    new()
    {
        Code = "ALM-001", Barcode = "6901234568001", Name = "Pain blanc 400g",
        Description = "Pain blanc de boulangerie, 400 grammes",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 1500, Unit = "pce",
        UnitPriceHtCdf = 1500, UnitPriceTtcCdf = 1500, UnitPriceHtUsd = 0.55m, UnitPriceTtcUsd = 0.55m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 50, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = true, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-002", Barcode = "6901234568002", Name = "Riz local 5kg",
        Description = "Riz blanc de production locale, sac de 5 kg",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 12000, Unit = "sac",
        UnitPriceHtCdf = 12000, UnitPriceTtcCdf = 12000, UnitPriceHtUsd = 4.36m, UnitPriceTtcUsd = 4.36m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 100, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-003", Barcode = "6901234568003", Name = "Huile de palme 1L",
        Description = "Huile de palme raffinée, bouteille 1 litre",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 8000, Unit = "btle",
        UnitPriceHtCdf = 8000, UnitPriceTtcCdf = 8000, UnitPriceHtUsd = 2.91m, UnitPriceTtcUsd = 2.91m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 60, MinStockLevel = 8, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-004", Barcode = "6901234568004", Name = "Sucre 1kg",
        Description = "Sucre blanc en poudre, paquet 1 kg",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 5000, Unit = "pqt",
        UnitPriceHtCdf = 5000, UnitPriceTtcCdf = 5000, UnitPriceHtUsd = 1.82m, UnitPriceTtcUsd = 1.82m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 80, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-005", Barcode = "6901234568005", Name = "Lait en poudre 400g",
        Description = "Lait entier en poudre, boîte 400 grammes",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 9500, Unit = "bte",
        UnitPriceHtCdf = 8190, UnitPriceTtcCdf = 9500, UnitPriceHtUsd = 2.98m, UnitPriceTtcUsd = 3.45m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 40, MinStockLevel = 5, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-006", Barcode = "6901234568006", Name = "Farine de maïs 2kg",
        Description = "Farine de maïs blanche, paquet 2 kg",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.A,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 6000, Unit = "pqt",
        UnitPriceHtCdf = 6000, UnitPriceTtcCdf = 6000, UnitPriceHtUsd = 2.18m, UnitPriceTtcUsd = 2.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 90, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "ALM-007", Barcode = "6901234568007", Name = "Cigarettes (paquet)",
        Description = "Paquet de 20 cigarettes, marque locale",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.C,
        SpecificTaxType = SpecificTaxType.FixedPerUnit, SpecificTaxValue = 500, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 5000, Unit = "pqt",
        UnitPriceHtCdf = 4310, UnitPriceTtcCdf = 5000, UnitPriceHtUsd = 1.57m, UnitPriceTtcUsd = 1.82m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 60, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },

    // ══════════════════════════════════════════
    //  🧴 HYGIÈNE
    // ══════════════════════════════════════════
    new()
    {
        Code = "HYG-001", Barcode = "6901234569001", Name = "Savon de toilette",
        Description = "Savon de toilette parfumé, 100 grammes",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 2000, Unit = "pce",
        UnitPriceHtCdf = 1724, UnitPriceTtcCdf = 2000, UnitPriceHtUsd = 0.63m, UnitPriceTtcUsd = 0.73m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catHygiene.Id,
        StockQuantity = 100, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "HYG-002", Barcode = "6901234569002", Name = "Papier hygiénique x4",
        Description = "Lot de 4 rouleaux de papier hygiénique",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 6000, Unit = "pqt",
        UnitPriceHtCdf = 5172, UnitPriceTtcCdf = 6000, UnitPriceHtUsd = 1.88m, UnitPriceTtcUsd = 2.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catHygiene.Id,
        StockQuantity = 70, MinStockLevel = 10, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "HYG-003", Barcode = "6901234569003", Name = "Eau de Javel 1L",
        Description = "Eau de Javel concentrée, bouteille 1 litre",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 4000, Unit = "btle",
        UnitPriceHtCdf = 3448, UnitPriceTtcCdf = 4000, UnitPriceHtUsd = 1.25m, UnitPriceTtcUsd = 1.45m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catHygiene.Id,
        StockQuantity = 50, MinStockLevel = 5, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "HYG-004", Barcode = "6901234569004", Name = "Dentifrice 100ml",
        Description = "Dentifrice fluoré, tube 100 ml",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 3500, Unit = "tube",
        UnitPriceHtCdf = 3017, UnitPriceTtcCdf = 3500, UnitPriceHtUsd = 1.10m, UnitPriceTtcUsd = 1.27m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catHygiene.Id,
        StockQuantity = 65, MinStockLevel = 8, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },

    // ══════════════════════════════════════════
    //  🔧 SERVICES
    // ══════════════════════════════════════════
    new()
    {
        Code = "SRV-001", Barcode = "", Name = "Consultation technique",
        Description = "Consultation technique sur site, facturation à l'heure",
        ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 50000, Unit = "h",
        UnitPriceHtCdf = 43103, UnitPriceTtcCdf = 50000, UnitPriceHtUsd = 15.67m, UnitPriceTtcUsd = 18.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catServices.Id,
        StockQuantity = 0, MinStockLevel = 0, TrackStock = false,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "SRV-002", Barcode = "", Name = "Installation logiciel",
        Description = "Installation et configuration de logiciel, forfait",
        ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 75000, Unit = "fft",
        UnitPriceHtCdf = 64655, UnitPriceTtcCdf = 75000, UnitPriceHtUsd = 23.51m, UnitPriceTtcUsd = 27.27m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catServices.Id,
        StockQuantity = 0, MinStockLevel = 0, TrackStock = false,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "SRV-003", Barcode = "", Name = "Maintenance mensuelle",
        Description = "Contrat de maintenance informatique mensuel",
        ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 150000, Unit = "mois",
        UnitPriceHtCdf = 129310, UnitPriceTtcCdf = 150000, UnitPriceHtUsd = 47.02m, UnitPriceTtcUsd = 54.55m,
        DefaultDiscountType = DiscountType.Percentage, DefaultDiscountValue = 10,
        CategoryId = catServices.Id,
        StockQuantity = 0, MinStockLevel = 0, TrackStock = false,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "SRV-004", Barcode = "", Name = "Formation utilisateur",
        Description = "Formation utilisateur sur site, facturation à la journée",
        ItemType = ItemType.SER, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 100000, Unit = "jour",
        UnitPriceHtCdf = 86207, UnitPriceTtcCdf = 100000, UnitPriceHtUsd = 31.35m, UnitPriceTtcUsd = 36.36m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catServices.Id,
        StockQuantity = 0, MinStockLevel = 0, TrackStock = false,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },

    // ══════════════════════════════════════════
    //  📎 BUREAU
    // ══════════════════════════════════════════
    new()
    {
        Code = "BUR-001", Barcode = "6901234570001", Name = "Ramette papier A4",
        Description = "Ramette de 500 feuilles papier blanc A4, 80g",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 15000, Unit = "ram",
        UnitPriceHtCdf = 12931, UnitPriceTtcCdf = 15000, UnitPriceHtUsd = 4.70m, UnitPriceTtcUsd = 5.45m,
        DefaultDiscountType = DiscountType.Percentage, DefaultDiscountValue = 5,
        CategoryId = catBureau.Id,
        StockQuantity = 30, MinStockLevel = 5, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BUR-002", Barcode = "6901234570002", Name = "Stylo bille bleu",
        Description = "Stylo à bille encre bleue, pointe moyenne",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 500, Unit = "pce",
        UnitPriceHtCdf = 431, UnitPriceTtcCdf = 500, UnitPriceHtUsd = 0.16m, UnitPriceTtcUsd = 0.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBureau.Id,
        StockQuantity = 200, MinStockLevel = 20, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BUR-003", Barcode = "6901234570003", Name = "Cartouche d'encre HP",
        Description = "Cartouche d'encre noire compatible HP LaserJet",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 45000, Unit = "pce",
        UnitPriceHtCdf = 38793, UnitPriceTtcCdf = 45000, UnitPriceHtUsd = 14.11m, UnitPriceTtcUsd = 16.36m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBureau.Id,
        StockQuantity = 10, MinStockLevel = 3, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "BUR-004", Barcode = "6901234570004", Name = "Classeur A4",
        Description = "Classeur à levier format A4, dos 80mm",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.B,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 8000, Unit = "pce",
        UnitPriceHtCdf = 6897, UnitPriceTtcCdf = 8000, UnitPriceHtUsd = 2.51m, UnitPriceTtcUsd = 2.91m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBureau.Id,
        StockQuantity = 25, MinStockLevel = 5, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },

    // ══════════════════════════════════════════
    //  🌍 EXPORT (TaxGroup D)
    // ══════════════════════════════════════════
    new()
    {
        Code = "EXP-001", Barcode = "6901234571001", Name = "Café vert export 60kg",
        Description = "Café vert arabica pour exportation, sac de 60 kg",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.D,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 450000, Unit = "sac",
        UnitPriceHtCdf = 450000, UnitPriceTtcCdf = 450000, UnitPriceHtUsd = 163.64m, UnitPriceTtcUsd = 163.64m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 20, MinStockLevel = 2, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "EXP-002", Barcode = "6901234571002", Name = "Cacao brut export 50kg",
        Description = "Fèves de cacao brut pour exportation, sac de 50 kg",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.D,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 380000, Unit = "sac",
        UnitPriceHtCdf = 380000, UnitPriceTtcCdf = 380000, UnitPriceHtUsd = 138.18m, UnitPriceTtcUsd = 138.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catAliments.Id,
        StockQuantity = 15, MinStockLevel = 2, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
    new()
    {
        Code = "EXP-003", Barcode = "6901234571003", Name = "Bois grume export m³",
        Description = "Bois en grume pour exportation, au mètre cube",
        ItemType = ItemType.BIE, TaxGroup = TaxGroup.D,
        SpecificTaxType = SpecificTaxType.None, SpecificTaxValue = 0, TaxSpecificMode = TaxSpecificMode.PerArticle,
        UnitPrice = 600000, Unit = "m3",
        UnitPriceHtCdf = 600000, UnitPriceTtcCdf = 600000, UnitPriceHtUsd = 218.18m, UnitPriceTtcUsd = 218.18m,
        DefaultDiscountType = DiscountType.None, DefaultDiscountValue = 0,
        CategoryId = catBureau.Id,
        StockQuantity = 5, MinStockLevel = 1, TrackStock = true,
        IsActive = true, IsFavorite = false, CreatedAt = DateTime.Now
    },
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