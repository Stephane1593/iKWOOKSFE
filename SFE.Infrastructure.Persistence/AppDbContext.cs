using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SFE.Application.Services;
using SFE.Domain.Abstractions;
using SFE.Domain.Common;
using SFE.Domain.Entities;
using SFE.Infrastructure.Persistence.Converters;

namespace SFE.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    // ═══ DbSets ═══
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<PointOfSale> PointsOfSale => Set<PointOfSale>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<PosStock> PosStocks { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<StockTransfer> StockTransfers { get; set; } = null!;
    public DbSet<StockTransferLine> StockTransferLines { get; set; } = null!;
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<DailyReport> DailyReports { get; set; } = null!;
    public DbSet<ReportInvoiceTypeSummary> ReportInvoiceTypeSummaries { get; set; } = null!;
    public DbSet<ReportTaxGroupDetail> ReportTaxGroupDetails { get; set; } = null!;
    public DbSet<ReportPaymentSummary> ReportPaymentSummaries { get; set; } = null!;
    public DbSet<ArticleReportLine> ArticleReportLines { get; set; } = null!;
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; } = null!;

    private readonly string _dbPath;
    private readonly ITimeProvider _time;
    private readonly ITenantProvider _tenant;

    // ── Design-time / fallback constructor ──
    public AppDbContext()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "sfe.db");

        _time = new SystemTimeProvider();
        _tenant = new TenantContext();
    }

    // ── Runtime constructor (DI) ──
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITimeProvider time,
        ITenantProvider tenant) : base(options)
    {
        _dbPath = string.Empty;
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder
                .UseSqlite($"Data Source={_dbPath};Cache=Shared")
                .AddInterceptors(new SqliteWalInterceptor());
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1) Toutes les IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 2) Conversion globale DateTimeOffset → long pour SQLite
        ApplyDateTimeOffsetConversions(modelBuilder);

        // 3) Global query filters (tenant + soft-delete)
        ApplyGlobalFilters(modelBuilder);
    }

    private static void ApplyDateTimeOffsetConversions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.GetValueConverter() != null) continue;

                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverters.ToTicks);
                    property.SetColumnType("INTEGER");
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(DateTimeOffsetConverters.ToNullableTicks);
                    property.SetColumnType("INTEGER");
                }
            }
        }
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        // NOTE: SyncableEntity and SyncableRootEntity are PARALLEL hierarchies
        // (neither inherits from the other), so the two branches are mutually
        // exclusive — no ordering trick needed.
        foreach (var et in modelBuilder.Model.GetEntityTypes())
        {
            var clr = et.ClrType;

            if (typeof(SyncableEntity).IsAssignableFrom(clr))
            {
                typeof(AppDbContext)
                    .GetMethod(nameof(SetTenantFilter),
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clr)
                    .Invoke(this, new object[] { modelBuilder });
            }
            else if (typeof(SyncableRootEntity).IsAssignableFrom(clr))
            {
                typeof(AppDbContext)
                    .GetMethod(nameof(SetRootSoftDeleteFilter),
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clr)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void SetTenantFilter<T>(ModelBuilder mb) where T : SyncableEntity
    {
        mb.Entity<T>().HasQueryFilter(e =>
            e.DeletedAtUtc == null &&
            (_tenant.IsBootstrapMode || e.CompanyId == _tenant.CompanyId));
    }

    private void SetRootSoftDeleteFilter<T>(ModelBuilder mb) where T : SyncableRootEntity
    {
        mb.Entity<T>().HasQueryFilter(e => e.DeletedAtUtc == null);
    }

    // ═══════════════════════════════════════════════════════════════
    // SaveChanges — applies stamps for BOTH hierarchies
    // ═══════════════════════════════════════════════════════════════

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditAndTenantStamps();
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndTenantStamps();
        return base.SaveChanges();
    }

    private void ApplyAuditAndTenantStamps()
    {
        var now = _time.UtcNow;

        // ── (1) Tenant-scoped entities (Product, Invoice, Client, …) ──
        foreach (var entry in ChangeTracker.Entries<SyncableEntity>())
            StampSyncable(entry, now);

        // ── (2) Tenant-root entity (Company) ──
        foreach (var entry in ChangeTracker.Entries<SyncableRootEntity>())
            StampRoot(entry, now);
    }

    private void StampSyncable(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SyncableEntity> entry,
        DateTimeOffset now)
    {
        var e = entry.Entity;

        switch (entry.State)
        {
            case EntityState.Added:
                if (e.SyncId == default) e.SyncId = Ulid.NewUlid();
                if (e.CreatedAtUtc == default) e.CreatedAtUtc = now;
                e.UpdatedAtUtc = now;
                if (e.Version == 0) e.Version = 1;

                // Stamp tenant
                if (e.CompanyId == 0 && !_tenant.IsBootstrapMode && _tenant.IsAuthenticated)
                    e.CompanyId = _tenant.CompanyId;

                // Stamp origin POS (only if not already set by business code)
                if (e.OriginPointOfSaleSyncId is null &&
                    _tenant.CurrentPointOfSaleSyncId is { } posSyncId)
                {
                    e.OriginPointOfSaleId = _tenant.CurrentPointOfSaleId;
                    e.OriginPointOfSaleSyncId = posSyncId;
                }
                break;

            case EntityState.Modified:
                e.MarkUpdated(now);
                break;

            case EntityState.Deleted:
                // Hard-delete → soft-delete rewrite
                entry.State = EntityState.Modified;
                e.MarkDeleted(now);
                break;
        }
    }

    private static void StampRoot(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SyncableRootEntity> entry,
        DateTimeOffset now)
    {
        var e = entry.Entity;

        switch (entry.State)
        {
            case EntityState.Added:
                if (e.SyncId == default) e.SyncId = Ulid.NewUlid();
                if (e.CreatedAtUtc == default) e.CreatedAtUtc = now;
                e.UpdatedAtUtc = now;
                if (e.Version == 0) e.Version = 1;
                break;

            case EntityState.Modified:
                e.MarkUpdated(now);
                break;

            case EntityState.Deleted:
                entry.State = EntityState.Modified;
                e.MarkDeleted(now);
                break;
        }
    }
}