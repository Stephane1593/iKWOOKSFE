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

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

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

        // Fallbacks design-time (migrations EF)
        _time = new SystemTimeProvider();
        _tenant = new TenantContext();
    }

    // ── DI constructor (runtime) ──
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

        // 1) Applique d'abord toutes les IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 2) Conversion GLOBALE DateTimeOffset → long pour SQLite
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
        foreach (var et in modelBuilder.Model.GetEntityTypes())
        {
            var clr = et.ClrType;

            // ⚠️ Ordre important : SyncableEntity hérite généralement de SyncableRootEntity
            //    donc tester le plus spécifique d'abord.
            if (typeof(SyncableEntity).IsAssignableFrom(clr))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(SetTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clr);
                method.Invoke(this, new object[] { modelBuilder });
            }
            else if (typeof(SyncableRootEntity).IsAssignableFrom(clr))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(SetRootSoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clr);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    // Tenant + soft-delete filter
    private void SetTenantFilter<T>(ModelBuilder mb) where T : SyncableEntity
    {
        mb.Entity<T>().HasQueryFilter(e =>
            e.DeletedAtUtc == null &&
            (_tenant.IsBootstrapMode || e.CompanyId == _tenant.CompanyId));
    }

    // Soft-delete filter only (root entities, non-tenant-scoped)
    private void SetRootSoftDeleteFilter<T>(ModelBuilder mb) where T : SyncableRootEntity
    {
        mb.Entity<T>().HasQueryFilter(e => e.DeletedAtUtc == null);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditAndTenantStamps();
        return await base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndTenantStamps();
        return base.SaveChanges();
    }

    private void ApplyAuditAndTenantStamps()
    {
        var now = _time.UtcNow;

        foreach (var entry in ChangeTracker.Entries<SyncableRootEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAtUtc == default)
                        entry.Entity.CreatedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    if (entry.Entity.SyncId == default)
                        entry.Entity.SyncId = Ulid.NewUlid();

                    // Stamp origin POS automatically
                    if (entry.Entity.OriginPointOfSaleSyncId is null &&
                        _tenant.CurrentPointOfSaleSyncId is { } posSyncId)
                    {
                        entry.Entity.OriginPointOfSaleId = _tenant.CurrentPointOfSaleId;
                        entry.Entity.OriginPointOfSaleSyncId = posSyncId;
                    }

                    // Stamp CompanyId on tenant-scoped entities
                    if (entry.Entity is SyncableEntity scoped &&
                        scoped.CompanyId == 0 &&
                        !_tenant.IsBootstrapMode &&
                        _tenant.IsAuthenticated)
                    {
                        scoped.CompanyId = _tenant.CompanyId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.MarkUpdated(now);
                    break;

                case EntityState.Deleted:
                    // Convert hard-delete to soft-delete
                    entry.State = EntityState.Modified;
                    entry.Entity.MarkDeleted(now);
                    break;
            }
        }
    }
}