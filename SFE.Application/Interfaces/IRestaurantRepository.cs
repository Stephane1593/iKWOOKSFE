using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IRestaurantRepository : IRepository<Restaurant>
{
    Task<Restaurant?> GetByIdWithDetailsAsync(int id);
}