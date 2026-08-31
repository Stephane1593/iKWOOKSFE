using SFE.Domain.Enums;

namespace SFE.Domain.Entities;

public class FiscalDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DeviceType DeviceType { get; set; }
    public bool IsActive { get; set; } = true;

    // ── MCF (physique) ──
    public string? ComPort { get; set; }        // ex: "COM3"
    public int BaudRate { get; set; } = 115200;

    // ── e-MCF (cloud) ──
    public string? ApiBaseUrl { get; set; }
    public string? ApiToken { get; set; }
    public DateTimeOffset? TokenExpiry { get; set; }

    // ── Infos récupérées du dispositif ──
    public string? NIM { get; set; }
    public string? NIF { get; set; }
    public int TotalTransactions { get; set; }
    public int SaleInvoiceCount { get; set; }
    public int CreditNoteCount { get; set; }
    public DateTimeOffset? LastConnectionToServer { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    // ── Taux de taxation configurés ──
    public decimal TaxRateA { get; set; } = 0m;
    public decimal TaxRateB { get; set; } = 16m;
    public decimal TaxRateC { get; set; } = 8m;
    public decimal TaxRateD { get; set; }
    public decimal TaxRateE { get; set; }
    public decimal TaxRateF { get; set; }
    public decimal TaxRateG { get; set; }
    public decimal TaxRateH { get; set; }
    public decimal TaxRateI { get; set; }
    public decimal TaxRateJ { get; set; }
    public decimal TaxRateK { get; set; }
    public decimal TaxRateL { get; set; }
    public decimal TaxRateM { get; set; }
    public decimal TaxRateN { get; set; }
    public decimal TaxRateO { get; set; }
    public decimal TaxRateP { get; set; }

    public decimal GetTaxRate(TaxGroup group) => group switch
    {
        TaxGroup.A => TaxRateA,
        TaxGroup.B => TaxRateB,
        TaxGroup.C => TaxRateC,
        TaxGroup.D => TaxRateD,
        TaxGroup.E => TaxRateE,
        TaxGroup.F => TaxRateF,
        TaxGroup.G => TaxRateG,
        TaxGroup.H => TaxRateH,
        TaxGroup.I => TaxRateI,
        TaxGroup.J => TaxRateJ,
        TaxGroup.K => TaxRateK,
        TaxGroup.L => TaxRateL,
        TaxGroup.M => TaxRateM,
        TaxGroup.N => TaxRateN,
        TaxGroup.O => TaxRateO,
        TaxGroup.P => TaxRateP,
        _ => 0m
    };
}