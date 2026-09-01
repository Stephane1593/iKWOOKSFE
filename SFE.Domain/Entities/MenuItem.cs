using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class MenuItem : SyncableEntity
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    public string Code { get; set; } = string.Empty; // optional product mapping
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsAvailable { get; set; } = true;
}