using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class MenuRepository :
Repository<Menu>,
IMenuRepository
{
    public MenuRepository(AppDbContext context)
    : base(context)
    {
    }

    public async Task<Menu?> GetByIdWithItemsAsync(int id)
    {
        return await _dbSet
        .AsNoTracking()
        .Include(m => m.Restaurant)
        .Include(m => m.Items)
        .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<Menu>> GetByRestaurantIdAsync(int restaurantId)
    {
        return await _dbSet
        .AsNoTracking()
        .Where(m => m.RestaurantId == restaurantId)
        .Include(m => m.Items)
        .OrderBy(m => m.Name)
        .ToListAsync();
    }
}