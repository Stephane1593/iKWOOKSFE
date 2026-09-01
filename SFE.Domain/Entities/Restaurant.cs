using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class Restaurant : SyncableEntity
{
    public int Id { get; set; }                    // inherited pattern used across the repo
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public List<Menu> Menus { get; set; } = new();
    public List<Table> Tables { get; set; } = new();
}