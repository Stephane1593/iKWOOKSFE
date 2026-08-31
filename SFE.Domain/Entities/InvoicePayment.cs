using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class InvoicePayment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Especes;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal CurrencyRate { get; set; }

    // ── Card terminal (Sunmi) transaction references ──
    public string? AuthCode { get; set; }        // acquirer authorization code
    public string? Rrn { get; set; }             // retrieval reference number
    public string? MaskedPan { get; set; }       // "**** **** **** 1234"
    public string? CardScheme { get; set; }      // VISA / MASTERCARD / …
    public string? TerminalId { get; set; }      // TID
    public string? TransactionRef { get; set; }  // acquirer transaction id (for void)
    public string? MobileOperator { get; set; }

    public Invoice Invoice { get; set; } = null!;
}