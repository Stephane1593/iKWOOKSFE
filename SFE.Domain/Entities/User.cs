// File: SFE.Domain/Entities/User.cs
namespace SFE.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // ═══════════════════════════════════════════════
    //  🆕 POS ASSIGNMENT (replaces AssignedPosIds JSON)
    // ═══════════════════════════════════════════════
    /// <summary>
    /// The POS (shop/location) this operator is assigned to.
    /// Null = unassigned or has access to ALL POS (admin/manager).
    /// </summary>
    public int? PointOfSaleId { get; set; }

    // ═══════════════════════════════════════════════
    //  NAVIGATION
    // ═══════════════════════════════════════════════
    public Role? Role { get; set; }
    public PointOfSale? PointOfSale { get; set; }
}