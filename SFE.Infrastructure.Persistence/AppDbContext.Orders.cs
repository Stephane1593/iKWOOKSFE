using Microsoft.EntityFrameworkCore;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence;

public partial class AppDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<KitchenPrinter> KitchenPrinters => Set<KitchenPrinter>();
}
