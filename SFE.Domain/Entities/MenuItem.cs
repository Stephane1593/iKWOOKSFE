using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public class MenuItem : SyncableEntity
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    // 🆕 Lien direct vers le catalogue (obligatoire ou optionnel)
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal? CustomPrice { get; set; }

    // Le prix ici peut surcharger le prix du catalogue (ex: Happy Hour)
    public decimal UnitPrice { get; set; }
    public bool IsAvailable { get; set; } = true;

    // Lien vers l'imprimante cuisine/bar
    public int? PrinterProfileId { get; set; }
    public PrinterProfile? PrinterProfile { get; set; }
}