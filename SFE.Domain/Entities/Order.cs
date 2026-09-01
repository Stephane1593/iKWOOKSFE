using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public enum OrderStatus { Open, InKitchen, Served, Paid, Closed, Voided }

public class Order : SyncableEntity
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public int? TableId { get; set; }     // null for table-less (takeaway / counter)
    public Table? Table { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public string OperatorId { get; set; } = string.Empty; // operator/user
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    // Totals cached for quick display / conversion
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
}