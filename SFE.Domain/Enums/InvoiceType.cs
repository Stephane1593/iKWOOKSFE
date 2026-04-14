namespace SFE.Domain.Enums;

/// <summary>
/// Types de factures selon la norme DGI-RDC.
/// </summary>
public enum InvoiceType
{
    /// <summary>Facture de Vente</summary>
    FV = 0,

    // Facture d'avoir (note de crédit)
    FA = 1,

    // Facture d'acompte
    FT = 2,

    // Facture de vente à l'exportation
    EV = 3,

    // Facture d'avoir à l'exportation
    EA = 4,

    // Facture d'acompte à l'exportation
    ET = 5
}

public static class InvoiceTypeExtensions
{
    public static bool IsSale(this InvoiceType t) => t is InvoiceType.FV or InvoiceType.FT or InvoiceType.EV or InvoiceType.ET;
    public static bool IsCreditNote(this InvoiceType t) => t is InvoiceType.FA or InvoiceType.EA;
    public static bool IsExport(this InvoiceType t) => t is InvoiceType.EV or InvoiceType.EA or InvoiceType.ET;
    public static string Label(this InvoiceType t) => t switch
    {
        InvoiceType.FV => "Facture de vente",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Facture de vente (export)",
        InvoiceType.EA => "Facture d'avoir (export)",
        InvoiceType.ET => "Facture d'acompte (export)",
        _ => t.ToString()
    };
}