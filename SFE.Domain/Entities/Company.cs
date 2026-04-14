using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NIF { get; set; } = string.Empty;
    public string ISF { get; set; } = string.Empty;          // 🆕 Identifiant Système de Facturation
    public string RCCM { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public byte[]? Logo { get; set; }                         // 🆕 Logo entreprise (PNG/JPEG)
    public PriceMode DefaultPriceMode { get; set; } = PriceMode.TTC;
    public bool LoyaltyEnabled { get; set; } = false;
    public decimal LoyaltyEarnRate { get; set; } = 1000m;
    public decimal LoyaltyRedeemRate { get; set; } = 500m;
    public DeploymentMode DeploymentMode { get; set; } = DeploymentMode.Standalone;

    // Navigation
    public List<PointOfSale> PointsOfSale { get; set; } = new();
}