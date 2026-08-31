using ClosedXML.Excel;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.Domain.Services;
using System.Drawing;
using System.Globalization;

namespace SFE.Application.Services;

public class ExcelInvoiceParser : IExcelInvoiceParser
{
    private readonly IUnitOfWork _uow;
    private readonly ITimeProvider _time;

    // Types autorisés en v1 (pas de notes de crédit ni proforma)
    private static readonly HashSet<InvoiceType> AllowedTypes = new()
    {
        InvoiceType.FV, InvoiceType.FT, InvoiceType.EV
    };

    private static readonly string[] RequiredHeaders =
    {
        "RefFacture","TypeFacture","ModePrix","Devise","TauxChange",
        "TypeClient","NIFClient","NomClient","AdresseClient","ContactClient","RCCMClient",
        "TypePaiement","CommentaireA","CommentaireB",
        "CodeArticle","Designation","Quantite","PrixUnitaire","GroupeTaxe",
        "TypeRemise","ValeurRemise","UniteMesure"
    };

    public ExcelInvoiceParser(IUnitOfWork uow, ITimeProvider time)
    {
        _uow = uow;
        _time = time;
    }

    public async Task<BulkParseResult> ParseAsync(
        Stream xlsxStream,
        int pointOfSaleId,
        string operatorId,
        string operatorName,
        CancellationToken ct = default)
    {
        var result = new BulkParseResult { PointOfSaleId = pointOfSaleId };

        using var wb = new XLWorkbook(xlsxStream);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Factures", StringComparison.OrdinalIgnoreCase));
        if (ws == null)
        {
            result.Errors.Add(new BulkParseError { Message = "Feuille 'Factures' introuvable." });
            return result;
        }

        // ── En-têtes ──
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
            result.Errors.Add(new BulkParseError
            {
                Message = "En-têtes manquantes : " + string.Join(", ", missing)
            });
            return result;
        }

        // Optional comment headers
        for (char c = 'C'; c <= 'H'; c++)
            colMap.TryAdd($"Commentaire{c}", 0);

        // ── Lecture des lignes ──
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < 2)
        {
            result.Errors.Add(new BulkParseError { Message = "Aucune donnée dans la feuille." });
            return result;
        }

        // Pré-chargement des produits pour éviter N+1
        var products = (await _uow.Products.GetAllAsync())
            .Where(p => p.IsActive)
            .ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        // Groupement par RefFacture (préserve l'ordre d'apparition)
        var groups = new List<(string Ref, List<(int Row, IXLRow Data)> Rows)>();
        var indexByRef = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int r = 2; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            var refCell = row.Cell(colMap["RefFacture"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(refCell))
            {
                result.Errors.Add(new BulkParseError
                {
                    ExcelRow = r,
                    Message = "Colonne 'RefFacture' vide."
                });
                continue;
            }

            if (!indexByRef.TryGetValue(refCell, out var idx))
            {
                idx = groups.Count;
                indexByRef[refCell] = idx;
                groups.Add((refCell, new List<(int, IXLRow)>()));
            }
            groups[idx].Rows.Add((r, row));
        }

        // ── Construction des Invoice ──
        foreach (var g in groups)
        {
            ct.ThrowIfCancellationRequested();
            var invoice = TryBuildInvoice(g.Ref, g.Rows, colMap, products,
                pointOfSaleId, operatorId, operatorName, result.Errors);

            if (invoice != null)
                result.Invoices.Add(invoice);
        }

        return result;
    }

    private Invoice? TryBuildInvoice(
        string reference,
        List<(int Row, IXLRow Data)> rows,
        Dictionary<string, int> col,
        Dictionary<string, Product> products,
        int posId,
        string operatorId,
        string operatorName,
        List<BulkParseError> errors)
    {
        var first = rows[0];
        var errorsBefore = errors.Count;

        // ── En-tête facture (première ligne du groupe) ──
        var typeStr = GetStr(first.Data, col, "TypeFacture");
        if (!Enum.TryParse<InvoiceType>(typeStr, true, out var type) || !AllowedTypes.Contains(type))
        {
            errors.Add(Err(first.Row, reference,
                $"TypeFacture invalide '{typeStr}'. Attendu: FV, FT ou EV."));
            return null;
        }

        var modeStr = GetStr(first.Data, col, "ModePrix");
        if (!Enum.TryParse<PriceMode>(modeStr, true, out var mode))
        {
            errors.Add(Err(first.Row, reference, $"ModePrix invalide '{modeStr}' (HT ou TTC)."));
            return null;
        }

        var clientTypeStr = GetStr(first.Data, col, "TypeClient");
        if (!Enum.TryParse<ClientType>(clientTypeStr, true, out var clientType))
        {
            errors.Add(Err(first.Row, reference, $"TypeClient invalide '{clientTypeStr}' (PP ou PM)."));
            return null;
        }

        var clientName = GetStr(first.Data, col, "NomClient");
        if (string.IsNullOrWhiteSpace(clientName))
        {
            errors.Add(Err(first.Row, reference, "NomClient obligatoire."));
            return null;
        }

        var clientNIF = GetStr(first.Data, col, "NIFClient");
        if (clientType == ClientType.PM && string.IsNullOrWhiteSpace(clientNIF))
        {
            errors.Add(Err(first.Row, reference, "NIFClient obligatoire pour un client PM."));
            return null;
        }

        var payStr = GetStr(first.Data, col, "TypePaiement");
        if (!Enum.TryParse<PaymentType>(payStr, true, out var payType))
        {
            errors.Add(Err(first.Row, reference,
                $"TypePaiement invalide '{payStr}'. Attendu: Especes, Virement, CarteBancaire, MobileMoney, Cheques, Credit, Autre."));
            return null;
        }

        var currency = GetStr(first.Data, col, "Devise");
        if (string.IsNullOrWhiteSpace(currency)) currency = "CDF";

        decimal rate = 1m;
        var rateStr = GetStr(first.Data, col, "TauxChange");
        if (!string.IsNullOrWhiteSpace(rateStr))
        {
            if (!decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out rate))
            {
                errors.Add(Err(first.Row, reference, $"TauxChange non numérique '{rateStr}'."));
                return null;
            }
        }
        if (!currency.Equals("CDF", StringComparison.OrdinalIgnoreCase) && rate <= 0)
        {
            errors.Add(Err(first.Row, reference, $"TauxChange requis pour devise {currency}."));
            return null;
        }
        if (currency.Equals("CDF", StringComparison.OrdinalIgnoreCase)) rate = 1m;

        var invoice = new Invoice
        {
            Type = type,
            Status = InvoiceStatus.Draft,
            PriceMode = mode,
            ClientType = clientType,
            ClientNIF = clientNIF,
            ClientName = clientName,
            ClientAddress = GetStr(first.Data, col, "AdresseClient"),
            ClientPhone = GetStr(first.Data, col, "ContactClient"),
            ClientRCCM = GetStr(first.Data, col, "RCCMClient"),
            OperatorId = operatorId,
            OperatorName = operatorName,
            PointOfSaleId = posId,
            CurrencyCode = currency.ToUpperInvariant(),
            CurrencyRate = rate,
            CurrencyDate = _time.UtcNow,
            CommentA = SafeComment(GetStr(first.Data, col, "CommentaireA"), $"Import Excel — Ref: {reference}"),
            CommentB = GetStr(first.Data, col, "CommentaireB"),
            CommentC = GetStr(first.Data, col, "CommentaireC"),
            CommentD = GetStr(first.Data, col, "CommentaireD"),
            CommentE = GetStr(first.Data, col, "CommentaireE"),
            CommentF = GetStr(first.Data, col, "CommentaireF"),
            CommentG = GetStr(first.Data, col, "CommentaireG"),
            CommentH = GetStr(first.Data, col, "CommentaireH"),
            DiscountBeforeTax = true,
            CreatedAt = _time.UtcNow
        };

        // ── Lignes ──
        int lineNo = 1;
        foreach (var (rowIdx, rowData) in rows)
        {
            var line = TryBuildLine(reference, rowIdx, rowData, col, products, mode, errors);
            if (line == null) continue;
            line.LineNumber = lineNo++;
            invoice.Lines.Add(line);
        }

        if (invoice.Lines.Count == 0)
        {
            errors.Add(Err(first.Row, reference, "Aucune ligne d'article valide pour cette facture."));
            return null;
        }

        // Recalcul provisoire pour connaître le total attendu (le service le refera)
        RecalculateForValidation(invoice);

        // ── Paiement (unique sur la première ligne) ──
        invoice.Payments.Add(new InvoicePayment
        {
            PaymentType = payType,
            Amount = invoice.TotalTTC,
            CurrencyCode = invoice.CurrencyCode,
            CurrencyRate = invoice.CurrencyRate
        });

        // Si des erreurs sont apparues, on abandonne
        if (errors.Count > errorsBefore) return null;

        return invoice;
    }

    private InvoiceLine? TryBuildLine(
        string reference,
        int rowIdx,
        IXLRow row,
        Dictionary<string, int> col,
        Dictionary<string, Product> products,
        PriceMode mode,
        List<BulkParseError> errors)
    {
        var code = GetStr(row, col, "CodeArticle");
        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add(Err(rowIdx, reference, "CodeArticle vide."));
            return null;
        }

        if (!products.TryGetValue(code, out var product))
        {
            errors.Add(Err(rowIdx, reference, $"Article '{code}' introuvable ou inactif."));
            return null;
        }

        var qtyStr = GetStr(row, col, "Quantite");
        if (!decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            errors.Add(Err(rowIdx, reference, $"Quantité invalide '{qtyStr}'."));
            return null;
        }

        // Prix override optionnel
        decimal unitPrice = product.UnitPrice;
        var priceStr = GetStr(row, col, "PrixUnitaire");
        if (!string.IsNullOrWhiteSpace(priceStr))
        {
            if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out unitPrice) || unitPrice < 0)
            {
                errors.Add(Err(rowIdx, reference, $"PrixUnitaire invalide '{priceStr}'."));
                return null;
            }
        }

        // Groupe taxe override
        var taxGroup = product.TaxGroup;
        var tgStr = GetStr(row, col, "GroupeTaxe");
        if (!string.IsNullOrWhiteSpace(tgStr))
        {
            if (!Enum.TryParse<TaxGroup>(tgStr, true, out taxGroup))
            {
                errors.Add(Err(rowIdx, reference, $"GroupeTaxe invalide '{tgStr}'."));
                return null;
            }
        }
        var taxRate = TaxCalculator.GetDefaultRate(taxGroup);

        // Remise
        var discType = DiscountType.None;
        decimal discValue = 0m;
        var dtStr = GetStr(row, col, "TypeRemise");
        if (!string.IsNullOrWhiteSpace(dtStr) && !Enum.TryParse<DiscountType>(dtStr, true, out discType))
        {
            errors.Add(Err(rowIdx, reference, $"TypeRemise invalide '{dtStr}'."));
            return null;
        }
        if (discType != DiscountType.None)
        {
            var dvStr = GetStr(row, col, "ValeurRemise");
            if (!decimal.TryParse(dvStr, NumberStyles.Any, CultureInfo.InvariantCulture, out discValue) || discValue < 0)
            {
                errors.Add(Err(rowIdx, reference, $"ValeurRemise invalide '{dvStr}'."));
                return null;
            }
        }

        // Prix HT/TTC à partir du prix saisi et du mode
        decimal unitHT, unitTTC;
        if (mode == PriceMode.TTC)
        {
            unitTTC = unitPrice;
            unitHT = TaxCalculator.R2(PriceModeConverter.TtcToHt(unitPrice, taxRate));
        }
        else
        {
            unitHT = unitPrice;
            unitTTC = TaxCalculator.R2(PriceModeConverter.HtToTtc(unitPrice, taxRate));
        }

        // Calcul complet via TaxCalculator (source de vérité)
        var calc = TaxCalculator.CalculateLineFull(new LineCalculationInput
        {
            UnitPriceHT = unitHT,
            UnitPriceTTC = unitTTC,
            Quantity = qty,
            TaxGroup = taxGroup,
            TaxGroupAType = taxGroup == TaxGroup.A ? TaxGroupAType.Exonere : null,
            TaxRate = taxRate,
            PriceMode = mode,
            DiscountType = discType,
            DiscountValue = discValue,
            DiscountBeforeTax = true,
            SpecificTaxType = product.SpecificTaxType,
            SpecificTaxValue = product.SpecificTaxValue,
            TaxApplicationMode = TaxApplicationMode.PerArticle,
            HasSpecificTax = product.SpecificTaxType != SpecificTaxType.None
        });

        var designation = GetStr(row, col, "Designation");
        var unit = GetStr(row, col, "UniteMesure");
        if (string.IsNullOrWhiteSpace(unit)) unit = product.Unit;

        return new InvoiceLine
        {
            ArticleId = product.Id,
            ProductId = product.Id,
            Code = product.Code,
            Name = string.IsNullOrWhiteSpace(designation) ? product.Name : designation,
            ItemType = product.ItemType,
            TaxGroup = taxGroup,
            TaxGroupAType = taxGroup == TaxGroup.A ? TaxGroupAType.Exonere : (TaxGroupAType?)null,
            TaxRate = taxRate,
            UnitPriceHT = unitHT,
            UnitPriceTTC = unitTTC,
            UnitPrice = unitPrice,
            OriginalPrice = unitPrice,
            Quantity = qty,
            Unit = unit,
            DiscountType = discType,
            DiscountValue = discValue,
            DiscountAmount = calc.DiscountAmount,
            HasSpecificTax = product.SpecificTaxType != SpecificTaxType.None,
            SpecificTaxType = product.SpecificTaxType,
            SpecificTaxValue = product.SpecificTaxValue,
            TaxApplicationMode = TaxApplicationMode.PerArticle,
            TaxSpecificAmount = calc.TaxSpecificAmount,
            GrossAmount = calc.GrossAmount,
            GrossAmountHT = calc.GrossAmountHT,
            GrossAmountTTC = calc.GrossAmountTTC,
            AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount,
            AmountHT = calc.AmountHT,
            AmountTVA = calc.AmountTVA,
            AmountTTC = calc.AmountTTC
        };
    }

    // Recalcul minimal pour poser un TotalTTC sur le paiement.
    // Le service refera un RecalculateTotals officiel dans NormalizeInvoiceAsync.
    private static void RecalculateForValidation(Invoice inv)
    {
        inv.TotalHT = TaxCalculator.R2(inv.Lines.Sum(l => l.AmountHT));
        inv.TotalTVA = TaxCalculator.R2(inv.Lines.Sum(l => l.AmountTVA));
        inv.TotalSpecificTax = TaxCalculator.R2(inv.Lines.Sum(l => l.TaxSpecificAmount));
        inv.TotalTTC = TaxCalculator.R2(inv.Lines.Sum(l => l.AmountTTC));
        inv.TotalDiscount = TaxCalculator.R2(inv.Lines.Sum(l => l.DiscountAmount));
        inv.TotalHTBeforeDiscount = TaxCalculator.R2(inv.Lines.Sum(l => l.AmountHTBeforeDiscount));
        inv.RemainingBalance = 0;
    }

    // ── Helpers ──
    private static string GetStr(IXLRow row, Dictionary<string, int> col, string name)
    {
        if (!col.TryGetValue(name, out var c) || c == 0) return "";
        return row.Cell(c).GetString().Trim();
    }

    private static string SafeComment(string raw, string fallback)
        => string.IsNullOrWhiteSpace(raw) ? fallback : raw;

    private static BulkParseError Err(int rowIdx, string reference, string msg) =>
        new() { ExcelRow = rowIdx, Reference = reference, Message = msg, Severity = BulkErrorSeverity.Error };

    // ── Template writer ──
    public Task WriteTemplateAsync(Stream output, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Factures");

        for (int i = 0; i < RequiredHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = RequiredHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(11, 61, 145);
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Exemple pré-rempli
        var sample = new object[]
        {
            "F-001","FV","TTC","CDF","1","PM","A1234567B","Client Exemple SARL",
            "Kinshasa","+243...", "CD/KIN/RCCM/22-B-1234",
            "Especes","","",
            "ART001","Article démo",2m,"1500","B","None","0","pce"
        };
        for (int i = 0; i < sample.Length; i++)
            ws.Cell(2, i + 1).Value = XLCellValue.FromObject(sample[i]);

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        var help = wb.AddWorksheet("Aide");
        help.Cell(1, 1).Value = "Colonnes acceptées : voir feuille 'Factures'. Types autorisés v1 : FV, FT, EV.";
        help.Cell(2, 1).Value = "Modes de prix : HT | TTC. Types de client : PP | PM. Devises : CDF (défaut) ou autre avec TauxChange.";
        help.Cell(3, 1).Value = "Groupes taxe : A à P (défaut = celui du produit). Types remise : None | Percent | Amount.";

        wb.SaveAs(output);
        return Task.CompletedTask;
    }
}