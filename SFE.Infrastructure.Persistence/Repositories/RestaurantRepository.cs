using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public class RestaurantRepository :
Repository<Restaurant>,
IRestaurantRepository
{
    public RestaurantRepository(AppDbContext context)
    : base(context)
    {
    }

    public async Task<Restaurant?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
        .AsNoTracking()
        .Include(r => r.Menus)
        .ThenInclude(m => m.Items)
        .Include(r => r.Tables)
        .FirstOrDefaultAsync(r => r.Id == id);
    }
}