using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class LoyaltyAccount
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public int TotalPointsEarned { get; set; } = 0;
    public int CurrentBalance { get; set; } = 0;
    public LoyaltyTierLevel TierLevel { get; set; } = LoyaltyTierLevel.Bronze;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }

    // Navigation
    public Client? Client { get; set; }
    public List<LoyaltyTransaction> Transactions { get; set; } = new();
}