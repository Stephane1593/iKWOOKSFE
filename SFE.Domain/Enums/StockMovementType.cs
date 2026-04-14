// File: SFE.Domain/Enums/StockMovementType.cs
namespace SFE.Domain.Enums;

public enum StockMovementType
{
    /// <summary>Entrée de stock (achat, réception fournisseur)</summary>
    Entry,

    /// <summary>Sortie manuelle (perte, casse, don)</summary>
    Exit,

    /// <summary>Ajustement d'inventaire (positif ou négatif)</summary>
    Adjustment,

    /// <summary>Transfert sortant vers un autre POS</summary>
    TransferOut,

    /// <summary>Transfert entrant depuis un autre POS</summary>
    TransferIn,

    /// <summary>Vente (décrément automatique à la normalisation)</summary>
    Sale,

    /// <summary>Retour/Avoir (ré-incrément automatique)</summary>
    CreditReturn,

    /// <summary>Inventaire physique (recomptage)</summary>
    PhysicalCount,

    /// <summary>Stock initial (première mise en stock)</summary>
    Initial
}