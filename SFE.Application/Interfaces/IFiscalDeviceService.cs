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

    /// <summary>
    /// Checks MCF's connection to DGI server (MCF command C2h).
    /// For e-MCF, uses the status endpoint.
    /// Returns last successful connection date + status.
    /// </summary>
    Task<FiscalServerConnectionResult> GetServerConnectionStatusAsync();

    /// <summary>
    /// Returns comprehensive device info for Dashboard/Settings display.
    /// MCF: combines C1h + C2h + 2Bh responses
    /// e-MCF: combines GET /api/invoice/ + GET /api/info/status + GET /api/info/taxGroups + GET /api/info/currencyRates
    /// </summary>
    Task<FiscalDeviceDetailedInfo> GetDetailedInfoAsync();
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

/// <summary>
/// Result of server connection check (MCF C2h / e-MCF status).
/// Used for 7-day disconnect notification per DGI spec §1.6.1.
/// </summary>
public class FiscalServerConnectionResult
{
    public bool Success { get; set; }
    public DateTime? LastServerConnection { get; set; }
    public string ConnectionStatus { get; set; } = "DIS"; // DIS/CON/TRA/RES
    public int TransactionsSent { get; set; }
    public int TransactionsPending { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LastError { get; set; }

    /// <summary>True if last server connection was more than 7 days ago.</summary>
    public bool IsOverSevenDays =>
        LastServerConnection.HasValue &&
        (DateTime.Now - LastServerConnection.Value).TotalDays > 7;
}

// ═══════════════════════════════════════════════════════════
// NEW RESULT CLASS — unified model for Dashboard + Settings
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Comprehensive fiscal device information — unified across MCF and e-MCF.
/// Used by DashboardViewModel (operational fields) and SettingsViewModel (configuration fields).
/// </summary>
public class FiscalDeviceDetailedInfo
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // ── Identity ─────────────────────────────────────────
    /// <summary>Machine ID (MCF: NID-ACNT, e-MCF: NIM)</summary>
    public string NIM { get; set; } = "";
    /// <summary>Taxpayer NIF</summary>
    public string NIF { get; set; } = "";
    /// <summary>Device type label: "MCF", "e-MCF", "Hybrid"</summary>
    public string DeviceTypeLabel { get; set; } = "";

    // ── Connection / Server Status ───────────────────────
    /// <summary>MCF: C2h STA (DIS/CON/TRA/RES), e-MCF: "CON" if status=true</summary>
    public string ConnectionStatus { get; set; } = "DIS";
    /// <summary>Display-friendly connection label</summary>
    public string ConnectionStatusDisplay => ConnectionStatus switch
    {
        "CON" => "Connecté",
        "TRA" => "Transmission en cours",
        "RES" => "Redémarrage",
        "DIS" => "Déconnecté",
        _ => ConnectionStatus
    };
    /// <summary>MCF C2h: last successful DGI connection. e-MCF: serverDateTime</summary>
    public DateTime? LastServerConnection { get; set; }
    /// <summary>True if last connection > 1 day ago (DGI spec alert)</summary>
    public bool IsConnectionStale =>
        LastServerConnection.HasValue &&
        (DateTime.Now - LastServerConnection.Value).TotalDays > 1;
    /// <summary>MCF C2h: last error description</summary>
    public string? LastError { get; set; }

    // ── Counters (DASHBOARD) ─────────────────────────────
    /// <summary>Total transaction counter (MCF: TC)</summary>
    public int TotalTransactions { get; set; }
    /// <summary>Sales invoice counter (MCF: FVC)</summary>
    public int SalesInvoiceCount { get; set; }
    /// <summary>Credit note counter (MCF: FRC)</summary>
    public int CreditNoteCount { get; set; }
    /// <summary>MCF C2h: transactions sent to DGI server</summary>
    public int TransactionsSent { get; set; }
    /// <summary>MCF C2h: transactions pending in device memory</summary>
    public int TransactionsInDevice { get; set; }
    /// <summary>e-MCF: pending (non-finalized) requests count</summary>
    public int PendingRequestsCount { get; set; }

    // ── Last Invoice (DASHBOARD) ─────────────────────────
    /// <summary>MCF C1h: DFDT — date/time of last invoice</summary>
    public DateTime? LastInvoiceDate { get; set; }
    /// <summary>MCF C1h: DFT — type of last invoice (FV, FA, etc.)</summary>
    public string? LastInvoiceType { get; set; }
    /// <summary>MCF C1h: DFS — Code DEF/DGI of last invoice</summary>
    public string? LastInvoiceCodeDEF { get; set; }
    /// <summary>MCF C1h: DFN — SFE invoice number of last invoice</summary>
    public string? LastInvoiceNumber { get; set; }
    /// <summary>MCF C1h: DMV — TTC amount of last invoice</summary>
    public decimal? LastInvoiceAmount { get; set; }

    // ── Tax Rates (SETTINGS) ─────────────────────────────
    /// <summary>Tax rates for groups A-P (16 values)</summary>
    public decimal[] TaxRates { get; set; } = new decimal[16];
    /// <summary>Helper: get rate by letter A-P</summary>
    public decimal GetTaxRate(char group) =>
        group >= 'A' && group <= 'P' ? TaxRates[group - 'A'] : 0;

    // ── Taxpayer Info (SETTINGS) ─────────────────────────
    /// <summary>MCF 2Bh I0 / e-MCF EmcfInfoDto.ShopName</summary>
    public string TaxpayerName { get; set; } = "";
    /// <summary>MCF 2Bh I1 / e-MCF EmcfInfoDto.Address1</summary>
    public string TaxpayerAddress { get; set; } = "";
    /// <summary>MCF 2Bh I2 / e-MCF EmcfInfoDto.Address3 (city)</summary>
    public string TaxpayerCity { get; set; } = "";
    /// <summary>MCF 2Bh I3 / e-MCF EmcfInfoDto.Contact1</summary>
    public string TaxpayerPhone { get; set; } = "";
    /// <summary>MCF 2Bh I4 / e-MCF EmcfInfoDto.Contact2</summary>
    public string TaxpayerEmail { get; set; } = "";

    // ── e-MCF Specific (SETTINGS) ────────────────────────
    /// <summary>API version (e-MCF only)</summary>
    public string? ApiVersion { get; set; }
    /// <summary>Token validity date (e-MCF only)</summary>
    public DateTime? TokenValidUntil { get; set; }
    /// <summary>Server date/time (e-MCF only)</summary>
    public DateTime? ServerDateTime { get; set; }
    /// <summary>e-MCF status: "Actif", "Enregistré", "Désactivé"</summary>
    public string? EmcfStatus { get; set; }
    /// <summary>Full list of e-MCF devices (e-MCF only)</summary>
    public List<EmcfDeviceInfo> EmcfDevices { get; set; } = new();

    // ── Currency (SETTINGS) ──────────────────────────────
    /// <summary>Current exchange rates from DGI</summary>
    public List<CurrencyRateInfo> CurrencyRates { get; set; } = new();

    // ── Current DateTime ─────────────────────────────────
    /// <summary>MCF C1h DT / e-MCF serverDateTime</summary>
    public DateTime? DeviceDateTime { get; set; }
}

/// <summary>e-MCF device info (from info/status emcfList)</summary>
public class EmcfDeviceInfo
{
    public string NIM { get; set; } = "";
    public string Status { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

/// <summary>Currency rate info from DGI</summary>
public class CurrencyRateInfo
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
}