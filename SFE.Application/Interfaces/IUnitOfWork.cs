namespace SFE.Application.Interfaces;
using SFE.Application.Events;


/// <summary>
/// Regroupe tous les repositories et gère les transactions.
/// Quand vous modifiez plusieurs entités, appelez SaveChangesAsync() une seule fois.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ICompanyRepository Companies { get; }
    IPointOfSaleRepository PointsOfSale { get; }
    IProductRepository Products { get; }
    IClientRepository Clients { get; }
    IUserRepository Users { get; }
    ILoyaltyAccountRepository LoyaltyAccounts { get; }
    IInvoiceRepository Invoices { get; }
    IProductCategoryRepository ProductCategories { get; }

    IAuditLogRepository AuditLogs { get; }

    // 🆕 Stock Multi-POS
    IPosStockRepository PosStocks { get; }
    IStockMovementRepository StockMovements { get; }
    IStockTransferRepository StockTransfers { get; }

    // inside IUnitOfWork:
    void EnqueueEvent(AppEvent evt, string? entityId = null);
    Task FlushEventsAsync();
    void ClearEvents();

    /// <summary>
    /// Enregistre tous les changements en attente dans la base de données.
    /// </summary>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Démarre une transaction explicite.
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Valide la transaction en cours.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Annule la transaction en cours.
    /// </summary>
    Task RollbackTransactionAsync();

    IRepository<T> GetRepository<T>() where T : class;

    /// <summary>🆕 Repository des paramètres applicatifs (remise, devise, etc.).</summary>
    IAppSettingsRepository AppSettings { get; }
}