// File: SFE.WPF/App.xaml.cs
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SFE.Application.Interfaces;
using SFE.Application.Services;
using SFE.Infrastructure.Persistence;
using SFE.Infrastructure.EMcf;
using SFE.WPF.ViewModels;
using SFE.WPF.Services;
using SFE.WPF.Views.Pages;
using SFE.WPF.Views;

namespace SFE.WPF;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        await InitializeDatabaseAsync();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ═══ Base de données ═══
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SFE");
        Directory.CreateDirectory(appDataPath);
        var dbPath = Path.Combine(appDataPath, "sfe.db");

        // ★ FIX: Cache=Shared + WAL interceptor
        var walInterceptor = new SqliteWalInterceptor();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared")
                   .AddInterceptors(walInterceptor),
            ServiceLifetime.Transient);

        // ═══ Repositories & Unit of Work ═══
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        // ═══ Services Application ═══
        services.AddTransient<SettingsService>();
        services.AddTransient<DashboardService>();
        services.AddTransient<InvoiceService>();
        services.AddTransient<ProductService>();
        services.AddTransient<ClientService>();
        services.AddTransient<StockService>();
        services.AddTransient<PointOfSaleService>();
        services.AddTransient<ReportService>();

        // ═══ Fiscal Device ═══
        services.AddSingleton<FiscalDeviceResolver>();
        services.AddSingleton<IFiscalDeviceService>(sp =>
            sp.GetRequiredService<FiscalDeviceResolver>());

        // ═══ ViewModels ═══
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<InvoicingViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<ClientsViewModel>();
        services.AddTransient<StockViewModel>();
        services.AddTransient<StockTransferViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SalesHistoryViewModel>();
        services.AddTransient<PointOfSaleManagementViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<InvoiceDocumentView>();

        // ═══ Fenêtres & Pages ═══
        services.AddTransient<MainWindow>();
        services.AddTransient<ClientsPage>();
        services.AddTransient<ReportView>();
        services.AddTransient<InvoiceDocumentView>();
    }

    private static async Task InitializeDatabaseAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseSeeder.SeedAsync(context);
    }
}