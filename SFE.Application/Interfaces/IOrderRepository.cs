using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByIdWithItemsAsync(int id);

    Task<List<Order>> GetPendingOrdersAsync(int restaurantId);

    Task AddItemAsync(int orderId, OrderItem item);

    Task<bool> RemoveItemAsync(int orderId, int orderItemId);

    Task<bool> ChangeStatusAsync(int orderId, OrderStatus status);
}