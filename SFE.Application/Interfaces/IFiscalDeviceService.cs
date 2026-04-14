namespace SFE.Application.Interfaces;

/// <summary>
/// Abstraction unifiée MCF (physique) et e-MCF (cloud).
/// Utilisée par InvoiceService — ne connaît pas le type de dispositif.
/// </summary>
public interface IFiscalDeviceService
{
    /// <summary>
    /// Soumet une facture au dispositif fiscal.
    /// MCF: enchaîne C3→C0→31h(×N)→36h→33h→35h
    /// e-MCF: POST /api/invoice/
    /// </summary>
    Task<FiscalSubmitResult> SubmitInvoiceAsync(FiscalInvoiceRequest request);

    /// <summary>
    /// Finalise (confirme) une facture soumise.
    /// MCF: commande 38h
    /// e-MCF: POST /api/invoice/{uid}/CONFIRM
    /// </summary>
    Task<FiscalFinalizeResult> FinalizeInvoiceAsync(string uid, decimal totalTTC, decimal totalTVA);

    /// <summary>
    /// Vérifie l'état du dispositif fiscal.
    /// MCF: commande C1h
    /// e-MCF: GET /api/invoice/
    /// </summary>
    Task<FiscalStatusResult> GetStatusAsync();

    /// <summary>
    /// Annule une facture en attente.
    /// MCF: commande 38h avec "C"
    /// e-MCF: POST /api/invoice/{uid}/CANCEL
    /// </summary>
    Task<bool> CancelPendingInvoiceAsync(string uid);
}

// ═══════════════════════════════════
// REQUEST — built by InvoiceService.BuildFiscalRequest()
// ═══════════════════════════════════

public class FiscalInvoiceRequest
{
    // Contribuable
    public string NIF { get; set; } = "";
    public string ISF { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string InvoiceType { get; set; } = "FV";
    public string PriceMode { get; set; } = "TTC";
    public string OperatorId { get; set; } = "";
    public string OperatorName { get; set; } = "";

    // Devise
    public string CurrencyCode { get; set; } = "CDF";
    public decimal? CurrencyRate { get; set; }
    public DateTime? CurrencyDate { get; set; }

    // Référence (factures d'avoir FA/EA)
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceDesc { get; set; }

    // Client
    public FiscalClientInfo? Client { get; set; }

    // Articles & Paiements
    public List<FiscalItemInfo> Items { get; set; } = new();
    public List<FiscalPaymentInfo> Payments { get; set; } = new();

    // Commentaires A-H
    public string? CommentA { get; set; }
    public string? CommentB { get; set; }
    public string? CommentC { get; set; }
    public string? CommentD { get; set; }
    public string? CommentE { get; set; }
    public string? CommentF { get; set; }
    public string? CommentG { get; set; }
    public string? CommentH { get; set; }
}

public class FiscalClientInfo
{
    public string? Type { get; set; }
    public string? TypeDesc { get; set; }
    public string? NIF { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Contact { get; set; }
}

public class FiscalItemInfo
{
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "BIE";
    public string TaxGroup { get; set; } = "A";
    public decimal TaxRate { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public string? TaxSpecificValue { get; set; }
    public decimal? TaxSpecificAmount { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? PriceModification { get; set; }
}

public class FiscalPaymentInfo
{
    public string Name { get; set; } = "ESPECES";
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? CurrencyRate { get; set; }
}

// ═══════════════════════════════════
// RESPONSES — consumed by InvoiceService.NormalizeInvoiceAsync()
// ═══════════════════════════════════

/// <summary>
/// Résultat de SubmitInvoiceAsync.
/// InvoiceService lit: Success, Uid, ErrorCode, ErrorMessage
/// </summary>
public class FiscalSubmitResult
{
    public bool Success { get; set; }
    public string? Uid { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Totaux globaux
    public decimal TotalTTC { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTS { get; set; }   // taxe spécifique
    public decimal TotalUSD { get; set; }   // MCUR

    // Ventilation par groupe fiscal (A–P)
    public Dictionary<string, decimal> GroupAmounts { get; set; } = new();  // MVA…MVP
    public Dictionary<string, decimal> GroupTVA { get; set; } = new();  // MTA…MTP
}

/// <summary>
/// Résultat de FinalizeInvoiceAsync.
/// InvoiceService lit: Success, CodeDEFDGI, QRCode, NIM, Counters, DateTime, ErrorCode, ErrorMessage
/// </summary>
public class FiscalFinalizeResult
{
    public bool Success { get; set; }
    public string? CodeDEFDGI { get; set; }
    public string? QRCode { get; set; }
    public string? NIM { get; set; }
    public string? Counters { get; set; }
    public string? DateTime { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Résultat de GetStatusAsync.
/// Utilisé par SettingsViewModel.TestConnection() et FiscalDeviceResolver
/// </summary>
public class FiscalStatusResult
{
    public bool Success { get; set; }
    public string? NIM { get; set; }
    public string? NIF { get; set; }
    public string? ErrorMessage { get; set; }

    public int PendingCount { get; set; }
    public List<PendingInvoiceInfo> PendingInvoices { get; set; } = new();
}

/// <summary>
/// Facture en attente sur le dispositif fiscal.
/// e-MCF : UID réel retourné par GET /api/invoice/
/// MCF   : UID = "MCF" (une seule facture ouverte à la fois)
/// </summary>
public class PendingInvoiceInfo
{
    public string Uid { get; set; } = "";
    public DateTime Date { get; set; }
    public string DateDisplay => Date == default ? "—" : Date.ToString("dd/MM/yyyy HH:mm");
}