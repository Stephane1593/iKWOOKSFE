using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }

    // === Identification ===
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceType Type { get; set; } = InvoiceType.FV;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public PriceMode PriceMode { get; set; } = PriceMode.TTC;
    public string ISF { get; set; } = string.Empty;

    // === Client ===
    public ClientType ClientType { get; set; } = ClientType.PP;
    public string ClientNIF { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientRCCM { get; set; } = string.Empty;

    // === Opérateur ===
    public string OperatorId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;

    // === Facture d'avoir (FA/EA) ===
    public int? OriginalInvoiceId { get; set; }
    public CreditNoteNature? CreditNoteNature { get; set; }
    public string OriginalInvoiceReference { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceDesc { get; set; } = string.Empty;

    // === 🆕 Facture d'acompte (FT/ET) — Chaîne d'avances ===
    /// <summary>
    /// Pour FT/ET : identifiant de groupe partagé entre les acomptes et la facture finale.
    /// Format libre, ex: "ADV-2026/001". Tous les FT du même groupe + le FV final partagent cette clé.
    /// </summary>
    public string? AdvanceGroupId { get; set; }

    /// <summary>
    /// Pour FV/EV finale : ID de la facture parente (optionnel, usage interne).
    /// </summary>
    public int? ParentInvoiceId { get; set; }
    public Invoice? ParentInvoice { get; set; }
    public List<Invoice> ChildInvoices { get; set; } = new();

    // === Devise ===
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal CurrencyRate { get; set; }
    public DateTime? CurrencyDate { get; set; }

    // === Commentaires (8 lignes A-H) ===
    public string CommentA { get; set; } = string.Empty;
    public string CommentB { get; set; } = string.Empty;
    public string CommentC { get; set; } = string.Empty;
    public string CommentD { get; set; } = string.Empty;
    public string CommentE { get; set; } = string.Empty;
    public string CommentF { get; set; } = string.Empty;
    public string CommentG { get; set; } = string.Empty;
    public string CommentH { get; set; } = string.Empty;

    // ══════════════════════════════════════════════
    // TOTAUX CALCULÉS
    // ══════════════════════════════════════════════
    public decimal TotalHTBeforeDiscount { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalSpecificTax { get; set; }
    public decimal TotalTTC { get; set; }

    public decimal TotalFixedSpecificTax { get; set; }
    public decimal TotalPercentSpecificTax { get; set; }

    // ══════════════════════════════════════════════
    // 🆕 ACOMPTES — Montants suivis
    // ══════════════════════════════════════════════
    /// <summary>Somme des acomptes déjà versés (calculé depuis ChildInvoices FT/ET).</summary>
    public decimal TotalAdvancesPaid { get; set; }
    /// <summary>Solde restant dû = TotalTTC - TotalAdvancesPaid.</summary>
    public decimal RemainingBalance { get; set; }

    // ══════════════════════════════════════════════
    // PARAMÈTRE SNAPSHOT
    // ══════════════════════════════════════════════
    public bool DiscountBeforeTax { get; set; } = true;

    // === Éléments de sécurité ===
    public string EmcfUid { get; set; } = string.Empty;
    public string CodeDEFDGI { get; set; } = string.Empty;
    public string QRCodeContent { get; set; } = string.Empty;
    public string NIM { get; set; } = string.Empty;
    public string Counters { get; set; } = string.Empty;
    public string DeviceDateTime { get; set; } = string.Empty;

    // === Dates ===
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? NormalizedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // === Relations ===
    public List<InvoiceLine> Lines { get; set; } = new();
    public List<InvoicePayment> Payments { get; set; } = new();
    public Invoice? OriginalInvoice { get; set; }

    // === Point de vente ===
    public int PointOfSaleId { get; set; }

    // ══════════════════════════════════════════════
    // 🆕 HELPERS
    // ══════════════════════════════════════════════
    public bool IsAdvanceInvoice => Type is InvoiceType.FT or InvoiceType.ET;
    public bool IsCreditNote => Type is InvoiceType.FA or InvoiceType.EA;
    public bool IsExport => Type is InvoiceType.EV or InvoiceType.EA or InvoiceType.ET;
    public bool IsFinalWithAdvances => (Type is InvoiceType.FV or InvoiceType.EV)
                                       && !string.IsNullOrEmpty(AdvanceGroupId);
}