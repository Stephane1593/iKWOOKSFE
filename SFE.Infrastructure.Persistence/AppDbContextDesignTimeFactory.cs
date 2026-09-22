using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SFE.Application.Services;
using SFE.Domain.Abstractions;

namespace SFE.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef migrations add / database update` at design time.
/// The runtime path builds AppDbContext through DI in App.xaml.cs; this
/// factory only exists so EF tooling can construct one without a running host.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 🚨 REMPLACEZ PAR VOTRE VRAI MOT DE PASSE POSTGRESQL ICI 🚨
        var connectionString = "Host=127.0.0.1;Database=SFE_Network_V2;Username=postgres;Password=Pass-123";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Stubs are fine for migrations: no data is read/written here.
        return new AppDbContext(options, new SystemTimeProvider(), new TenantContext());
    }
}