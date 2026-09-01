using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;

namespace SFE.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository
: Repository<Order>, IOrderRepository
{
    private readonly ITimeProvider _time;

    public OrderRepository(
    AppDbContext context,
    ITimeProvider time)
    : base(context)
    {
        _time = time;
    }

    public async Task<Order?> GetByIdWithItemsAsync(int id)
{
    return await _dbSet
    .AsNoTracking()
    .Include(order => order.Items)
    .FirstOrDefaultAsync(order => order.Id == id);
}

public async Task<List<Order>> GetPendingOrdersAsync(
int restaurantId)
{
    var pendingStatuses = new[]
    {
OrderStatus.Open,
OrderStatus.InKitchen,
OrderStatus.Served
};

    return await _dbSet
    .AsNoTracking()
    .Where(order =>
    order.RestaurantId == restaurantId &&
    pendingStatuses.Contains(order.Status))
    .Include(order => order.Items)
    .OrderBy(order => order.Id)
    .ToListAsync();
}

public async Task AddItemAsync(
int orderId,
OrderItem item)
{
    ArgumentNullException.ThrowIfNull(item);

    if (item.Quantity <= 0)
    {
        throw new ArgumentOutOfRangeException(
        nameof(item.Quantity),
        item.Quantity,
        "Order item quantity must be greater than zero.");
    }

    if (item.UnitPrice < 0)
    {
        throw new ArgumentOutOfRangeException(
        nameof(item.UnitPrice),
        item.UnitPrice,
        "Order item unit price cannot be negative.");
    }

    var order = await _dbSet
    .Include(existingOrder => existingOrder.Items)
    .FirstOrDefaultAsync(existingOrder =>
    existingOrder.Id == orderId);

    if (order is null)
    {
        throw new KeyNotFoundException(
        $"Order with ID {orderId} was not found.");
    }

    EnsureOrderCanBeModified(order);

    item.OrderId = order.Id;
    item.LineTotal = CalculateLineTotal(item);

    order.Items.Add(item);

    RecalculateOrderTotal(order);
    Touch(order);
}

public async Task<bool> RemoveItemAsync(
int orderId,
int orderItemId)
{
    var order = await _dbSet
    .Include(existingOrder => existingOrder.Items)
    .FirstOrDefaultAsync(existingOrder =>
    existingOrder.Id == orderId);

    if (order is null)
    {
        return false;
    }

    EnsureOrderCanBeModified(order);

    var item = order.Items.FirstOrDefault(existingItem =>
    existingItem.Id == orderItemId);

    if (item is null)
    {
        return false;
    }

    order.Items.Remove(item);

    RecalculateOrderTotal(order);
    Touch(order);

    return true;
}

public async Task<bool> ChangeStatusAsync(
int orderId,
OrderStatus status)
{
    var order = await _dbSet
    .FirstOrDefaultAsync(existingOrder =>
    existingOrder.Id == orderId);

    if (order is null)
    {
        return false;
    }

    if (order.Status == status)
    {
        return true;
    }

    ValidateStatusTransition(order.Status, status);

    order.Status = status;
    Touch(order);

    return true;
}

private static decimal CalculateLineTotal(OrderItem item)
{
    return item.UnitPrice * item.Quantity;
}

private static void RecalculateOrderTotal(Order order)
{
    order.TotalTTC = order.Items.Sum(item =>
    CalculateLineTotal(item));
}

private static void EnsureOrderCanBeModified(Order order)
{
    if (order.Status is OrderStatus.Paid
    or OrderStatus.Closed
    or OrderStatus.Voided)
    {
        throw new InvalidOperationException(
        $"Order {order.Id} cannot be modified because " +
        $"its current status is {order.Status}.");
    }
}

private static void ValidateStatusTransition(
OrderStatus currentStatus,
OrderStatus newStatus)
{
    if (newStatus == OrderStatus.Voided)
    {
        if (currentStatus == OrderStatus.Closed)
        {
            throw new InvalidOperationException(
            "A closed order cannot be voided.");
        }

        return;
    }

    var isValid = currentStatus switch
    {
        OrderStatus.Open =>
        newStatus == OrderStatus.InKitchen,

        OrderStatus.InKitchen =>
        newStatus == OrderStatus.Served,

        OrderStatus.Served =>
        newStatus == OrderStatus.Paid,

        OrderStatus.Paid =>
        newStatus == OrderStatus.Closed,

        OrderStatus.Closed => false,

        OrderStatus.Voided => false,

        _ => false
    };

    if (!isValid)
    {
        throw new InvalidOperationException(
        $"Invalid order status transition from " +
        $"{currentStatus} to {newStatus}.");
    }
}

private void Touch(Order order)
{
    order.UpdatedAtUtc = _time.UtcNow;
}
}