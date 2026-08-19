using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public interface IBulkInvoiceService
{
    Task<BulkParseResult> ParseAndValidateAsync(
        Stream xlsxStream,
        int pointOfSaleId,
        string operatorId,
        string operatorName,
        CancellationToken ct = default);

    Task<BulkExecutionResult> ExecuteAsync(
        BulkParseResult parsed,
        IProgress<BulkProgress>? progress = null,
        CancellationToken ct = default);
}

public class BulkParseResult
{
    public List<Invoice> Invoices { get; } = new();
    public List<BulkParseError> Errors { get; } = new();
    public int PointOfSaleId { get; set; }
    public bool IsValid => Errors.All(e => e.Severity == BulkErrorSeverity.Warning) && Invoices.Count > 0;
}

public class BulkParseError
{
    public int? ExcelRow { get; set; }
    public string? Reference { get; set; }
    public string Message { get; set; } = "";
    public BulkErrorSeverity Severity { get; set; } = BulkErrorSeverity.Error;
}

public enum BulkErrorSeverity { Warning, Error }

public class BulkExecutionResult
{
    public List<BulkRowResult> Results { get; } = new();
    public int SuccessCount => Results.Count(r => r.Success);
    public int FailureCount => Results.Count(r => !r.Success);
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public bool WasCancelled { get; set; }
    public TimeSpan Elapsed => CompletedAt - StartedAt;
}

public class BulkRowResult
{
    public string Reference { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string ClientName { get; set; } = "";
    public decimal TotalTTC { get; set; }
    public bool Success { get; set; }
    public int? InvoiceId { get; set; }
    public string? CodeDEFDGI { get; set; }
    public string? QRCodeContent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public class BulkProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentReference { get; set; } = "";
    public string Phase { get; set; } = ""; // "processing" | "success" | "failure" | "done"
    public string? LastError { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public TimeSpan Elapsed { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
}