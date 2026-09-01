using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class Menu : SyncableEntity
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<MenuItem> Items { get; set; } = new();
}