namespace SFE.Domain.Enums;

/// <summary>
/// Mode de saisie des prix. Toggle disponible dans les paramètres (défaut)
/// et dans chaque facture (avant ajout du premier article).
/// </summary>
public enum PriceMode
{
    /// <summary>Prix saisis Toutes Taxes Comprises</summary>
    TTC = 0,

    /// <summary>Prix saisis Hors Taxes</summary>
    HT = 1
}