namespace SFE.Domain.Entities;

public class LoyaltyTransaction
{
    public int Id { get; set; }
    public int LoyaltyAccountId { get; set; }
    public int? InvoiceId { get; set; }
    public string Type { get; set; } = "EARN"; // EARN or REDEEM
    public int Points { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // Navigation
    public LoyaltyAccount? LoyaltyAccount { get; set; }
}