namespace SFE.Domain.Enums;

/// <summary>
/// Les 16 groupes de taxation définis par la DGI-RDC.
/// </summary>
public enum TaxGroup
{

    A = 0,  // Exonéré / Hors champ — TVA 0%
    B = 1,  // Taxable standard — TVA 16%
    C = 2,  // Taux réduit — TVA 5%
    D = 3,  // Régime dérogatoire — TVA 0%
    E = 4,  // Exportation — TVA 0%
    F = 5,  // Marché public ext. — TVA 16%
    G = 6,  // Marché public ext. — TVA 5%
    H = 7,  // Consignation — TVA 0%
    I = 8,  // Garantie/Caution — TVA 0%
    J = 9,  // Débours — TVA 0%
    K = 10, // Non assujettis — TVA 0%
    L = 11, // Prélèvements — (article type TAX uniquement)
    M = 12, // Ventes réglementées — HT seul
    N = 13, // TVA spécifique — (article type TAX uniquement)
    O = 14, // Taux réduit — TVA 1%
    P = 15  // Marché public ext. — TVA 1%
}

