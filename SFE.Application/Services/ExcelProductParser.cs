using ClosedXML.Excel;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.Domain.Services;
using System.Globalization;

namespace SFE.Application.Services;

public class ExcelProductParser : IExcelProductParser
{
    private readonly ITimeProvider _time;

    private static readonly string[] RequiredHeaders =
    {
        "Code", "CodeBarres", "Nom", "Description", "TypeArticle", "GroupeTaxe",
        "ModePrix", "PrixUnitaire", "UniteMesure", "Categorie",
        "SuiviStock", "QuantiteStock", "StockMinimum"
    };

    public ExcelProductParser(ITimeProvider time)
    {
        _time = time;
    }

    public Task<BulkProductParseResult> ParseAsync(Stream xlsxStream, CancellationToken ct = default)
    {
        var result = new BulkProductParseResult();
        using var wb = new XLWorkbook(xlsxStream);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Produits", StringComparison.OrdinalIgnoreCase));

        if (ws == null)
        {
            result.Errors.Add(new BulkParseError { Message = "Feuille 'Produits' introuvable." });
            return Task.FromResult(result);
        }

        var headerRow = ws.Row(1);
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c <= headerRow.LastCellUsed()?.Address.ColumnNumber; c++)
        {
            var h = headerRow.Cell(c).GetString().Trim();
            if (!string.IsNullOrEmpty(h)) colMap[h] = c;
        }

        var missing = RequiredHeaders.Where(h => !colMap.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            result.Errors.Add(new BulkParseError { Message = "En-têtes manquantes : " + string.Join(", ", missing) });
            return Task.FromResult(result);
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 2; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            var name = GetStr(row, colMap, "Nom");
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new BulkParseError { ExcelRow = r, Message = "Le 'Nom' est obligatoire." });
                continue;
            }

            var typeStr = GetStr(row, colMap, "TypeArticle");
            if (!Enum.TryParse<ItemType>(typeStr, true, out var itemType)) itemType = ItemType.BIE;

            var tgStr = GetStr(row, colMap, "GroupeTaxe");
            if (!Enum.TryParse<TaxGroup>(tgStr, true, out var taxGroup)) taxGroup = TaxGroup.B;

            var modeStr = GetStr(row, colMap, "ModePrix");
            if (!Enum.TryParse<PriceMode>(modeStr, true, out var priceMode)) priceMode = PriceMode.TTC;

            var priceStr = GetStr(row, colMap, "PrixUnitaire");
            if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price < 0)
            {
                result.Errors.Add(new BulkParseError { ExcelRow = r, Message = "PrixUnitaire invalide." });
                continue;
            }

            var taxRate = TaxCalculator.GetDefaultRate(taxGroup);
            decimal unitHT, unitTTC;
            if (priceMode == PriceMode.TTC)
            {
                unitTTC = price;
                unitHT = TaxCalculator.R2(PriceModeConverter.TtcToHt(price, taxRate));
            }
            else
            {
                unitHT = price;
                unitTTC = TaxCalculator.R2(PriceModeConverter.HtToTtc(price, taxRate));
            }

            var trackStr = GetStr(row, colMap, "SuiviStock");
            bool trackStock = trackStr.Equals("OUI", StringComparison.OrdinalIgnoreCase) || trackStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

            decimal stockQty = 0, minStock = 0;
            if (trackStock)
            {
                decimal.TryParse(GetStr(row, colMap, "QuantiteStock"), NumberStyles.Any, CultureInfo.InvariantCulture, out stockQty);
                decimal.TryParse(GetStr(row, colMap, "StockMinimum"), NumberStyles.Any, CultureInfo.InvariantCulture, out minStock);
            }

            var product = new Product
            {
                Code = GetStr(row, colMap, "Code"),
                Barcode = GetStr(row, colMap, "CodeBarres"),
                Name = name,
                Description = GetStr(row, colMap, "Description"),
                ItemType = itemType,
                TaxGroup = taxGroup,
                TaxGroupAType = taxGroup == TaxGroup.A ? TaxGroupAType.Exonere : null,
                UnitPrice = unitHT,
                UnitPriceHtCdf = unitHT,
                UnitPriceTtcCdf = unitTTC,
                Unit = GetStr(row, colMap, "UniteMesure"),
                TrackStock = trackStock,
                StockQuantity = stockQty,
                MinStockLevel = minStock,
                IsActive = true,
                CreatedAt = _time.UtcNow.UtcDateTime
            };

            if (string.IsNullOrWhiteSpace(product.Unit)) product.Unit = "pce";

            result.Products.Add(product);

            var catName = GetStr(row, colMap, "Categorie");
            if (!string.IsNullOrWhiteSpace(catName))
                result.ExcelCategoryNames[product] = catName;
        }

        return Task.FromResult(result);
    }

    public Task WriteTemplateAsync(Stream output, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Produits");

        for (int i = 0; i < RequiredHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = RequiredHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(11, 61, 145);
            cell.Style.Font.FontColor = XLColor.White;
        }

        var sample = new object[]
        {
            "P-001", "123456789", "Produit Exemple", "Description courte", "BIE", "B",
            "TTC", "1500", "pce", "Alimentation",
            "OUI", "50", "5"
        };

        for (int i = 0; i < sample.Length; i++)
            ws.Cell(2, i + 1).Value = XLCellValue.FromObject(sample[i]);

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(output);
        return Task.CompletedTask;
    }

    private static string GetStr(IXLRow row, Dictionary<string, int> col, string name)
    {
        if (!col.TryGetValue(name, out var c) || c == 0) return "";
        return row.Cell(c).GetString().Trim();
    }
}