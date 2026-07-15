namespace SFE.Application.Interfaces;



public class SunmiChargeRequest
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "CDF";
    /// <summary>Numéro de facture SFE — sert de référence marchand.</summary>
    public string Reference { get; set; } = "";
    public string OperatorId { get; set; } = "";
}

public class SunmiChargeResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public string? AuthCode { get; set; }
    public string? Rrn { get; set; }
    public string? MaskedPan { get; set; }
    public string? CardScheme { get; set; }
    public string? TerminalId { get; set; }
    public string? TransactionRef { get; set; }
    public decimal ApprovedAmount { get; set; }
}

public class SunmiVoidResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SunmiStatusResult
{
    public bool Success { get; set; }
    public string? TerminalId { get; set; }
    public string? ErrorMessage { get; set; }
}