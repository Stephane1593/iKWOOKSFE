using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

public class BulkProductParseResult
{
    public List<Product> Products { get; set; } = new();
    public List<BulkParseError> Errors { get; set; } = new();
    public Dictionary<Product, string> ExcelCategoryNames { get; set; } = new();
}

public interface IExcelProductParser
{
    Task<BulkProductParseResult> ParseAsync(Stream xlsxStream, CancellationToken ct = default);
    Task WriteTemplateAsync(Stream output, CancellationToken ct = default);
}