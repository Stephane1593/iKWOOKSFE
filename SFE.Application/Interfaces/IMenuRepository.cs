using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IMenuRepository : IRepository<Menu>
{
    Task<Menu?> GetByIdWithItemsAsync(int id);

    Task<List<Menu>> GetByRestaurantIdAsync(int restaurantId);
}