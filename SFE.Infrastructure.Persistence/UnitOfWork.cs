using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SFE.Application.Events;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Infrastructure.Persistence.Repositories;

namespace SFE.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    // 🔒 Un seul writer à la fois pour SQLite
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _holdingLock;

    // ✅ Pending events — accumulated during the transaction, fired after commit
    private readonly List<AppEventArgs> _pendingEvents = new();

    // Repositories (créés à la demande — "lazy initialization")
    private ICompanyRepository? _companies;
    private IPointOfSaleRepository? _pointsOfSale;
    private IProductRepository? _products;
    private IClientRepository? _clients;
    private IUserRepository? _users;
    private ILoyaltyAccountRepository? _loyaltyAccounts;
    private IInvoiceRepository? _invoices;
    private IProductCategoryRepository? _productCategories;
    private IAppSettingsRepository? _appSettings;

    // 🆕 Stock Multi-POS
    public IPosStockRepository PosStocks { get; }
    public IStockMovementRepository StockMovements { get; }
    public IStockTransferRepository StockTransfers { get; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;

        PosStocks = new PosStockRepository(context);
        StockMovements = new StockMovementRepository(context);
        StockTransfers = new StockTransferRepository(context);
    }

    public IInvoiceRepository Invoices =>
        _invoices ??= new InvoiceRepository(_context);

    public ICompanyRepository Companies =>
        _companies ??= new CompanyRepository(_context);

    public IPointOfSaleRepository PointsOfSale =>
        _pointsOfSale ??= new PointOfSaleRepository(_context);

    public IProductRepository Products =>
        _products ??= new ProductRepository(_context);

    public IProductCategoryRepository ProductCategories =>
        _productCategories ??= new ProductCategoryRepository(_context);

    public IClientRepository Clients =>
        _clients ??= new ClientRepository(_context);

    public IUserRepository Users =>
        _users ??= new UserRepository(_context);

    public ILoyaltyAccountRepository LoyaltyAccounts =>
        _loyaltyAccounts ??= new LoyaltyAccountRepository(_context);

    public IAppSettingsRepository AppSettings =>
        _appSettings ??= new AppSettingsRepository(_context);

    // ── Enqueue events (services call this instead of publishing directly) ──

    public void EnqueueEvent(AppEvent evt, string? entityId = null)
    {
        _pendingEvents.Add(new AppEventArgs { Event = evt, EntityId = entityId });
    }

    // ── SaveChanges — auto-clears tracker & fires events ──────────

    public async Task<int> SaveChangesAsync()
    {
        if (_holdingLock)
        {
            // Inside a transaction — just save, don't fire events yet
            return await _context.SaveChangesAsync();
        }

        // Standalone save (no explicit transaction)
        await _writeLock.WaitAsync();
        try
        {
            var result = await _context.SaveChangesAsync();

            // ✅ Clear tracker so next reads are fresh
            _context.ChangeTracker.Clear();

            // ✅ Fire all pending events AFTER successful save
            await FlushEventsAsync();

            return result;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Transaction avec verrouillage global ──────────────────────

    public async Task BeginTransactionAsync()
    {
        await _writeLock.WaitAsync();
        _holdingLock = true;
        try
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }
        catch
        {
            _holdingLock = false;
            _writeLock.Release();
            throw;
        }
    }

    public async Task CommitTransactionAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }

            _context.ChangeTracker.Clear();   // ← force fresh reads from DB
            await FlushEventsAsync();         // ← publish all queued events
        }
        finally
        {
            ReleaseLock();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
            _context.ChangeTracker.Clear();
            ClearEvents();
        }
        finally
        {
            ReleaseLock();
        }
    }

    // ── Flush events ──────────────────────────────────────────────

    public async Task FlushEventsAsync()
    {
        if (_pendingEvents.Count == 0) return;

        // Deduplicate: same (Event, EntityId) pair fires only once
        var distinct = _pendingEvents
            .GroupBy(e => (e.Event, e.EntityId))
            .Select(g => g.First())
            .ToList();
        _pendingEvents.Clear();

        foreach (var e in distinct)
            await AppEventBus.PublishAsync(e);
    }

    // ── Dispose ───────────────────────────────────────────────────

    public void ClearEvents() => _pendingEvents.Clear();

    public void Dispose()
    {
        _pendingEvents.Clear();
        _transaction?.Dispose();
        _transaction = null;
        ReleaseLock();
        _context.Dispose();
    }

    private void ReleaseLock()
    {
        if (_holdingLock)
        {
            _holdingLock = false;
            _writeLock.Release();
        }
    }

    public IRepository<T> GetRepository<T>() where T : class
    {
        return new Repository<T>(_context);
    }
}