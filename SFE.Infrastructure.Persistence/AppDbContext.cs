// File: SFE.Infrastructure/Persistence/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence;

public class AppDbContext : DbContext
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
    public DbSet<DailyReport> DailyReports { get; set; }
    public DbSet<ReportInvoiceTypeSummary> ReportInvoiceTypeSummaries { get; set; }
    public DbSet<ReportTaxGroupDetail> ReportTaxGroupDetails { get; set; }
    public DbSet<ReportPaymentSummary> ReportPaymentSummaries { get; set; }
    public DbSet<ArticleReportLine> ArticleReportLines { get; set; }

    private readonly string _dbPath;

    // ── Design-time / fallback constructor ──
    public AppDbContext()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "sfe.db");
    }

    // ── DI constructor (used at runtime) ──
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback (design-time) — also gets WAL + busy_timeout
            optionsBuilder
                .UseSqlite($"Data Source={_dbPath};Cache=Shared")
                .AddInterceptors(new SqliteWalInterceptor());
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}