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

    public Invoice Invoice { get; set; } = null!;
}