namespace SFE.Domain.Enums;

/// <summary>
/// Cycle de vie d'une facture dans le SFE.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>
    /// Brouillon — édition libre, aucun envoi au MCF/eMCF.
    /// État initial, et état permanent des proformas non-converties.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Envoyée au MCF/eMCF (POST de la demande de facture),
    /// en attente de la demande de finalisation (CONFIRM ou CANCEL).
    /// État intermédiaire éphémère (≤ 2 min côté eMCF).
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Normalisée par le dispositif fiscal — Code DEF/DGI obtenu,
    /// QR code généré, facture juridiquement émise.
    /// État terminal pour les factures fiscales.
    /// </summary>
    Normalized = 2,

    /// <summary>
    /// Annulée — soit avant normalisation (action CANCEL côté eMCF),
    /// soit côté SFE pour un brouillon abandonné.
    /// État terminal.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Erreur lors de la normalisation (rejet eMCF, panne MCF,
    /// délai dépassé). Permet rejeu manuel ou investigation.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Spécifique aux proformas — la proforma a été convertie en
    /// facture fiscale (FV ou FT). Le document original est figé
    /// pour audit ; la nouvelle facture porte une référence vers lui.
    /// </summary>
    Converted = 5
}

public static class InvoiceStatusExtensions
{
    /// <summary>
    /// L'état est terminal : la facture ne peut plus changer.
    /// </summary>
    public static bool IsTerminal(this InvoiceStatus s) =>
        s is InvoiceStatus.Normalized
          or InvoiceStatus.Cancelled
          or InvoiceStatus.Converted;

    /// <summary>
    /// L'état autorise l'édition des lignes / totaux.
    /// </summary>
    public static bool IsEditable(this InvoiceStatus s) =>
        s is InvoiceStatus.Draft or InvoiceStatus.Error;

    /// <summary>
    /// Transition légale Draft → Pending → (Normalized | Cancelled | Error).
    /// Draft → Converted réservé aux proformas.
    /// </summary>
    public static bool CanTransitionTo(this InvoiceStatus from, InvoiceStatus to) =>
        (from, to) switch
        {
            (InvoiceStatus.Draft, InvoiceStatus.Pending) => true,
            (InvoiceStatus.Draft, InvoiceStatus.Cancelled) => true,
            (InvoiceStatus.Draft, InvoiceStatus.Converted) => true,   // proforma only
            (InvoiceStatus.Pending, InvoiceStatus.Normalized) => true,
            (InvoiceStatus.Pending, InvoiceStatus.Cancelled) => true,
            (InvoiceStatus.Pending, InvoiceStatus.Error) => true,
            (InvoiceStatus.Error, InvoiceStatus.Pending) => true,     // retry
            (InvoiceStatus.Error, InvoiceStatus.Cancelled) => true,   // give up
            _ => false
        };

    public static string Label(this InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft => "Brouillon",
        InvoiceStatus.Pending => "En attente",
        InvoiceStatus.Normalized => "Normalisée",
        InvoiceStatus.Cancelled => "Annulée",
        InvoiceStatus.Error => "Erreur",
        InvoiceStatus.Converted => "Convertie",
        _ => s.ToString()
    };
}