namespace SFE.Domain.Enums;

public enum CreditNoteNature
{
    /// <summary>Correction d'erreur</summary>
    COR = 0,

    /// <summary>Retour/annulation totale</summary>
    RAN = 1,

    /// <summary>Retour marchandises</summary>
    RAM = 2,

    /// <summary>Rabais, Remises, Ristournes</summary>
    RRR = 3
}