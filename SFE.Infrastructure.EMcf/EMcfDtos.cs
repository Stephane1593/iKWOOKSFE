using System.Text.Json.Serialization;

namespace SFE.Infrastructure.EMcf;

// ═══════════════════════════════════════════════════════════
// DTOs — conformes au protocole e-MCF-SFE + DGI 2026
// ═══════════════════════════════════════════════════════════

// ── 1.1 Status ───────────────────────────────────────────
public class StatusResponseDto
{
    public bool Status { get; set; }
    public string Version { get; set; } = "";
    public string Nif { get; set; } = "";
    public string Nim { get; set; } = "";
    public DateTimeOffset TokenValid { get; set; }        // 🆕
    public DateTimeOffset ServerDateTime { get; set; }    // 🆕
    public int PendingRequestsCount { get; set; }
    public List<PendingRequestDto> PendingRequestsList { get; set; } = new();
}

public class PendingRequestDto
{
    public DateTimeOffset Date { get; set; }              // 🆕
    public string Uid { get; set; } = "";
}

// ── 1.2 Invoice Request ──────────────────────────────────
public class InvoiceRequestDataDto
{
    public string Nif { get; set; } = "";
    public string Rn { get; set; } = "";
    public string Mode { get; set; } = "ht";
    public string Isf { get; set; } = "";
    public string Type { get; set; } = "FV";
    public List<ItemDto> Items { get; set; } = new();
    public ClientDto Client { get; set; } = new();
    public OperatorDto Operator { get; set; } = new();
    public List<PaymentDto> Payment { get; set; } = new();
    public string Reference { get; set; } = "";
    public string ReferenceType { get; set; } = "";
    public string ReferenceDesc { get; set; } = "";
    public string Cmta { get; set; } = "";
    public string Cmtb { get; set; } = "";
    public string Cmtc { get; set; } = "";
    public string Cmtd { get; set; } = "";
    public string Cmte { get; set; } = "";
    public string Cmtf { get; set; } = "";
    public string Cmtg { get; set; } = "";
    public string Cmth { get; set; } = "";
    public string CurCode { get; set; } = "CDF";
    public string CurDate { get; set; } = "";   // sérialisée en ISO par le mapper
    public decimal CurRate { get; set; }
}

public class ItemDto
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "BIE";
    public string Name { get; set; } = "";
    public string Price { get; set; } = "0";
    public decimal Quantity { get; set; }
    public string TaxGroup { get; set; } = "A";
    public string TaxSpecificValue { get; set; } = "0%";
    public decimal TaxSpecificAmount { get; set; }
    public decimal OriginalPrice { get; set; }
    public string PriceModification { get; set; } = "";
}

public class ClientDto
{
    public string Nif { get; set; } = "";
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Address { get; set; } = "";
    public string Type { get; set; } = "PP";
    public string TypeDesc { get; set; } = "Personne physique";
}

public class OperatorDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class PaymentDto
{
    public string Name { get; set; } = "ESPECES";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "CDF";
    public decimal CurrencyRate { get; set; }
}

// ── 1.2 Invoice Response ─────────────────────────────────
public class InvoiceResponseDataDto
{
    public string? Uid { get; set; }

    public decimal Ta { get; set; }
    public decimal Tb { get; set; }
    public decimal Tc { get; set; }
    public decimal Td { get; set; }
    public decimal Te { get; set; }
    public decimal Tf { get; set; }
    public decimal Tg { get; set; }
    public decimal Th { get; set; }
    public decimal Ti { get; set; }
    public decimal Tj { get; set; }
    public decimal Tk { get; set; }
    public decimal Tl { get; set; }
    public decimal Tm { get; set; }
    public decimal Tn { get; set; }
    public decimal To { get; set; }
    public decimal Tp { get; set; }

    public decimal Taa { get; set; }
    public decimal Tab { get; set; }
    public decimal Tac { get; set; }
    public decimal Tad { get; set; }
    public decimal Tae { get; set; }
    public decimal Taf { get; set; }
    public decimal Tag { get; set; }
    public decimal Tah { get; set; }
    public decimal Tai { get; set; }
    public decimal Taj { get; set; }
    public decimal Tak { get; set; }
    public decimal Tal { get; set; }
    public decimal Tam { get; set; }
    public decimal Tan { get; set; }
    public decimal Tao { get; set; }
    public decimal Tap { get; set; }

    public decimal Haa { get; set; }
    public decimal Hab { get; set; }
    public decimal Hac { get; set; }
    public decimal Had { get; set; }
    public decimal Hae { get; set; }
    public decimal Haf { get; set; }
    public decimal Hag { get; set; }
    public decimal Hah { get; set; }
    public decimal Hai { get; set; }
    public decimal Haj { get; set; }
    public decimal Hak { get; set; }
    public decimal Hal { get; set; }
    public decimal Ham { get; set; }
    public decimal Han { get; set; }
    public decimal Hao { get; set; }
    public decimal Hap { get; set; }

    public decimal Vaa { get; set; }
    public decimal Vab { get; set; }
    public decimal Vac { get; set; }
    public decimal Vad { get; set; }
    public decimal Vae { get; set; }
    public decimal Vaf { get; set; }
    public decimal Vag { get; set; }
    public decimal Vah { get; set; }
    public decimal Vai { get; set; }
    public decimal Vaj { get; set; }
    public decimal Vak { get; set; }
    public decimal Val { get; set; }
    public decimal Vam { get; set; }
    public decimal Van { get; set; }
    public decimal Vao { get; set; }
    public decimal Vap { get; set; }

    public decimal Ts { get; set; }
    public decimal Total { get; set; }
    public decimal Vtotal { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorDesc { get; set; }
}

// ── 1.3 Finalize ─────────────────────────────────────────
public class FinalizeInvoiceRequestDataDto
{
    public decimal Total { get; set; }
    public decimal Vtotal { get; set; }
}

public class FinalizeInvoiceResponseDataDto
{
    [JsonPropertyName("dateTime")]
    public string? InvoiceDateTime { get; set; }

    public string? QrCode { get; set; }
    public string? CodeDEFDGI { get; set; }
    public string? Counters { get; set; }
    public string? Nim { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDesc { get; set; }
}

// ── 2.x Info ─────────────────────────────────────────────
public class InfoResponseDto
{
    public bool Status { get; set; }
    public string Version { get; set; } = "";
    public string Nif { get; set; } = "";
    public string Nim { get; set; } = "";
    public DateTimeOffset TokenValid { get; set; }        // 🆕
    public DateTimeOffset ServerDateTime { get; set; }    // 🆕
    public List<EmcfInfoDto> EmcfList { get; set; } = new();
}

public class EmcfInfoDto
{
    public string Nim { get; set; } = "";
    public string Status { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string Address1 { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string Address3 { get; set; } = "";
    public string Contact1 { get; set; } = "";
    public string Contact2 { get; set; } = "";
    public string Contact3 { get; set; } = "";
}

public class TaxGroupsDto
{
    public decimal A { get; set; }
    public decimal B { get; set; }
    public decimal C { get; set; }
    public decimal D { get; set; }
    public decimal E { get; set; }
    public decimal F { get; set; }
    public decimal G { get; set; }
    public decimal H { get; set; }
    public decimal I { get; set; }
    public decimal J { get; set; }
    public decimal K { get; set; }
    public decimal L { get; set; }
    public decimal M { get; set; }
    public decimal N { get; set; }
    public decimal O { get; set; }
    public decimal P { get; set; }

    public decimal[] ToArray() => new[]
    { A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P };
}

public class InvoiceTypeDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class PaymentTypeDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ClientTypeDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ReferenceTypeDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ItemTypeDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public class CurrencyRateDto
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset Date { get; set; }              // 🆕
    public decimal Rate { get; set; }
}