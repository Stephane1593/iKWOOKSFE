

namespace SFE.Domain.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int? MenuItemId { get; set; }    // optional link to MenuItem
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal LineTotal { get; set; }  // UnitPrice * Quantity minus discounts
        public string? Notes { get; set; }      // e.g. "no onions"
    }
}
