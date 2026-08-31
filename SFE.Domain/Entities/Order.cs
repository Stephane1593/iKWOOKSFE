using System;
using System.Collections.Generic;

namespace SFE.Domain.Entities
{
    public enum OrderStatus { Draft = 0, Open = 1, SentToKitchen = 2, Closed = 3, Cancelled = 4 }

    public class Table
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int Number { get; set; }
        public string State { get; set; } = "Free"; // Free, Occupied, Reserved
    }

    public class Order
    {
        public int Id { get; set; }
        public int TableId { get; set; }
        public Table? Table { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Financials
        public decimal ServiceChargePercent { get; set; }
        public decimal TipAmount { get; set; }

        // Relations
        public List<OrderLine> Lines { get; set; } = new();
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        // Reuse Product/InvoiceLine fields where sensible
        public int? ProductId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string? ModifiersJson { get; set; }
    }
}
