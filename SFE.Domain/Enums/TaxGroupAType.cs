namespace SFE.Domain.Enums;

/// <summary>
/// Distingue la nature du groupe A (TVA 0 %).
/// Affichage : Exonere → "A", HorsChamp → "A-HC".
/// Non-breaking : null équivaut à Exonéré.
/// </summary>
public enum TaxGroupAType
{
    Exonere = 0,
    HorsChamp = 1
}