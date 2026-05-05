namespace SFE.Domain.Enums;

/// <summary>
/// Types de factures selon la norme DGI-RDC.
/// </summary>
public enum InvoiceType
{
    /// <summary>Facture de Vente</summary>
    FV = 0,

    /// <summary>Facture d'avoir (note de crédit)</summary>
    FA = 1,

    /// <summary>Facture d'acompte</summary>
    FT = 2,

    /// <summary>Facture de vente à l'exportation</summary>
    EV = 3,

    /// <summary>Facture d'avoir à l'exportation</summary>
    EA = 4,

    /// <summary>Facture d'acompte à l'exportation</summary>
    ET = 5,

    /// <summary>
    /// Facture proforma (devis / pré-facture).
    /// Non normalisée, non transmise à la DGI.
    /// </summary>
    PRO = 6
}

public static class InvoiceTypeExtensions
{
    public static bool IsSale(this InvoiceType t) =>
        t is InvoiceType.FV or InvoiceType.FT or InvoiceType.EV or InvoiceType.ET;

    public static bool IsCreditNote(this InvoiceType t) =>
        t is InvoiceType.FA or InvoiceType.EA;

    public static bool IsExport(this InvoiceType t) =>
        t is InvoiceType.EV or InvoiceType.EA or InvoiceType.ET;

    public static bool IsAdvance(this InvoiceType t) =>
        t is InvoiceType.FT or InvoiceType.ET;

    /// <summary>
    /// Vrai pour les types qui ne doivent PAS passer par le dispositif fiscal
    /// (proforma uniquement pour l'instant).
    /// </summary>
    public static bool IsProforma(this InvoiceType t) =>
        t is InvoiceType.PRO;

    /// <summary>
    /// Vrai pour les types qui DOIVENT être normalisés par le MCF.
    /// </summary>
    public static bool RequiresFiscalNormalization(this InvoiceType t) =>
        !t.IsProforma();

    public static string Label(this InvoiceType t) => t switch
    {
        InvoiceType.FV => "Facture de vente",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Facture de vente (export)",
        InvoiceType.EA => "Facture d'avoir (export)",
        InvoiceType.ET => "Facture d'acompte (export)",
        InvoiceType.PRO => "Facture proforma",
        _ => t.ToString()
    };

    /// <summary>
    /// Mention en majuscules à imprimer en grand sur le document.
    /// </summary>
    public static string DisplayBanner(this InvoiceType t) => t switch
    {
        InvoiceType.FV => "FACTURE DE VENTE",
        InvoiceType.FA => "FACTURE D'AVOIR",
        InvoiceType.FT => "FACTURE D'ACOMPTE",
        InvoiceType.EV => "FACTURE DE VENTE",
        InvoiceType.EA => "FACTURE D'AVOIR",
        InvoiceType.ET => "FACTURE D'ACOMPTE",
        InvoiceType.PRO => "FACTURE PROFORMA",
        _ => "FACTURE"
    };
}