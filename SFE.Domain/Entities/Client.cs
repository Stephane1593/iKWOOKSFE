using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class Client
{
    public int Id { get; set; }
    public ClientType Type { get; set; } = ClientType.PP;
    public string? NIF { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RCCM { get; set; }
    public bool IsLoyaltyMember { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public LoyaltyAccount? LoyaltyAccount { get; set; }
}