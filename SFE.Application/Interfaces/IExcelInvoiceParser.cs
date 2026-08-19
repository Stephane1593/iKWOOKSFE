namespace SFE.Application.Interfaces;

public interface IExcelInvoiceParser
{
    Task<BulkParseResult> ParseAsync(
        Stream xlsxStream,
        int pointOfSaleId,
        string operatorId,
        string operatorName,
        CancellationToken ct = default);

    /// <summary>Writes an empty template with headers + data validation.</summary>
    Task WriteTemplateAsync(Stream output, CancellationToken ct = default);
}