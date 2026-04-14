namespace SFE.Domain.Enums;

/// <summary>
/// Types de client — DGI-RDC 2026 (Annexe I).
/// </summary>
public enum ClientType
{
    /// <summary>Personne physique — tous champs facultatifs</summary>
    PP = 0,

    /// <summary>Personne morale — Dénomination + NIF obligatoires</summary>
    PM = 1,

    /// <summary>Personne physique commerçante — Nom + NIF obligatoires</summary>
    PC = 2,

    /// <summary>Profession libérale — Nom + NIF obligatoires</summary>
    PL = 3,

    /// <summary>Ambassades et Organisations internationales — Nom + Réf. document obligatoires</summary>
    AO = 4
}