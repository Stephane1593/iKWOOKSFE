using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class InvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly StockService _stockService;
    private readonly IAuditService _auditService;

    public InvoiceService(IUnitOfWork unitOfWork, IFiscalDeviceService fiscalDevice, StockService stockService, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _fiscalDevice = fiscalDevice;
        _stockService = stockService;
        _auditService = auditService;
    }

    public async Task<string> GenerateInvoiceNumberAsync(InvoiceType type)
    {
        return await _unitOfWork.Invoices.GenerateNextInvoiceNumberAsync(type, DateTime.Now.Year);
    }

    // ══════════════════════════════════════════════════════════
    //  LOOKUP FACTURE ORIGINALE (pour FA/EA)
    // ══════════════════════════════════════════════════════════

    public async Task<Invoice?> LookupOriginalInvoiceAsync(string codeDEFDGI)
    {
        if (string.IsNullOrWhiteSpace(codeDEFDGI))
            return null;

        var invoice = await _unitOfWork.Invoices.GetByCodeDEFDGIAsync(codeDEFDGI.Trim());

        if (invoice == null || invoice.Status != InvoiceStatus.Normalized)
            return null;

        if (invoice.Type.IsCreditNote())
            return null;

        return invoice;
    }

    // ══════════════════════════════════════════════════════════
    //  CUMUL DES QUANTITÉS DÉJÀ REMBOURSÉES (pour FA/EA)
    // ══════════════════════════════════════════════════════════

    public async Task<Dictionary<string, decimal>> GetCumulativeRefundedQuantitiesAsync(string originalCodeDEFDGI)
    {
        var creditNotes = await _unitOfWork.Invoices.GetCreditNotesForOriginalAsync(originalCodeDEFDGI);
        var cumulative = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var cn in creditNotes)
        {
            foreach (var line in cn.Lines)
            {
                var key = line.Code;
                if (string.IsNullOrEmpty(key)) continue;

                if (cumulative.ContainsKey(key))
                    cumulative[key] += line.Quantity;
                else
                    cumulative[key] = line.Quantity;
            }
        }

        return cumulative;
    }

    // ══════════════════════════════════════════════════════════
    //  AVANCES — Récupérer les FT/ET d'un groupe
    // ══════════════════════════════════════════════════════════

    public async Task<List<Invoice>> GetAdvancesForGroupAsync(string advanceGroupId)
    {
        if (string.IsNullOrWhiteSpace(advanceGroupId))
            return new List<Invoice>();

        return await _unitOfWork.Invoices.GetAdvancesByGroupAsync(advanceGroupId);
    }

    public async Task<decimal> GetTotalAdvancesPaidAsync(string advanceGroupId)
    {
        var advances = await GetAdvancesForGroupAsync(advanceGroupId);
        return advances.Sum(a => a.TotalTTC);
    }

    public string GenerateAdvanceGroupId()
    {
        return $"ADV-{DateTime.Now.Year}/{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    // ══════════════════════════════════════════════════════════
    //  NORMALISATION — Flux complet
    // ══════════════════════════════════════════════════════════

    public async Task<NormalizationResult> NormalizeInvoiceAsync(Invoice invoice)
    {
        RecalculateTotals(invoice);

        var validation = await ValidateInvoiceAsync(invoice);
        if (!validation.IsValid)
            return new NormalizationResult { Success = false, ErrorMessage = validation.ErrorMessage };

        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company == null)
            return new NormalizationResult { Success = false, ErrorMessage = "Entreprise non configurée." };

        var request = BuildFiscalRequest(invoice, company);

        invoice.Status = InvoiceStatus.Pending;
        var submitResult = await _fiscalDevice.SubmitInvoiceAsync(request);

        if (!submitResult.Success)
        {
            invoice.Status = InvoiceStatus.Error;
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Erreur dispositif fiscal [{submitResult.ErrorCode}]: {submitResult.ErrorMessage}"
            };
        }

        invoice.EmcfUid = submitResult.Uid ?? "";

        if (submitResult.TotalTTC > 0 &&
            (Math.Abs(submitResult.TotalTTC - invoice.TotalTTC) > 0.01m ||
             Math.Abs(submitResult.TotalTVA - invoice.TotalTVA) > 0.01m))
        {
            await _fiscalDevice.CancelPendingInvoiceAsync(invoice.EmcfUid);
            invoice.Status = InvoiceStatus.Error;
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Divergence montants — SFE TTC:{invoice.TotalTTC:F2} vs Dispositif:{submitResult.TotalTTC:F2}, " +
                               $"SFE TVA:{invoice.TotalTVA:F2} vs Dispositif:{submitResult.TotalTVA:F2}"
            };
        }

        var finalizeResult = await _fiscalDevice.FinalizeInvoiceAsync(
            invoice.EmcfUid, invoice.TotalTTC, invoice.TotalTVA);

        if (!finalizeResult.Success)
        {
            invoice.Status = InvoiceStatus.Error;
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Erreur finalisation [{finalizeResult.ErrorCode}]: {finalizeResult.ErrorMessage}"
            };
        }

        invoice.CodeDEFDGI = finalizeResult.CodeDEFDGI ?? "";
        invoice.QRCodeContent = finalizeResult.QRCode ?? "";
        invoice.NIM = finalizeResult.NIM ?? "";
        invoice.Counters = finalizeResult.Counters ?? "";
        invoice.DeviceDateTime = finalizeResult.DateTime ?? "";
        invoice.Status = InvoiceStatus.Normalized;
        invoice.NormalizedAt = DateTime.Now;

        await ApplyStockMovementsAsync(invoice);

        await _unitOfWork.Invoices.AddAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        var auditAction = invoice.IsCreditNote ? AuditAction.CreditNoteNormalized
            : invoice.IsAdvanceInvoice ? AuditAction.AdvanceInvoiceNormalized
            : AuditAction.InvoiceNormalized;
        await _auditService.LogInvoiceAsync(auditAction, invoice);

        return new NormalizationResult
        {
            Success = true,
            InvoiceId = invoice.Id,
            CodeDEFDGI = invoice.CodeDEFDGI,
            QRCodeContent = invoice.QRCodeContent
        };
    }

    // ══════════════════════════════════════════════════════════
    //  RECALCUL DES TOTAUX — V13: Two-component TVA in TTC mode
    // ══════════════════════════════════════════════════════════

    public void RecalculateTotals(Invoice invoice)
    {
        bool isTTC = invoice.PriceMode == PriceMode.TTC;

        // ═══════════════════════════════════════════════════════
        //  Pass 1 : CalculateLineFull for every line
        // ═══════════════════════════════════════════════════════
        foreach (var line in invoice.Lines)
        {
            var input = new LineCalculationInput
            {
                UnitPriceHT = line.UnitPriceHT,
                UnitPriceTTC = line.UnitPriceTTC,
                Quantity = line.Quantity,
                TaxGroup = line.TaxGroup,
                TaxRate = line.TaxRate,
                PriceMode = invoice.PriceMode,
                DiscountType = line.DiscountType,
                DiscountValue = line.DiscountValue,
                DiscountBeforeTax = invoice.DiscountBeforeTax,
                SpecificTaxType = line.SpecificTaxType,
                SpecificTaxValue = line.SpecificTaxValue,
                TaxApplicationMode = line.TaxApplicationMode
            };

            var calc = TaxCalculator.CalculateLineFull(input);

            line.AmountHTBeforeDiscount = calc.AmountHTBeforeDiscount;
            line.DiscountAmount = calc.DiscountAmount;
            line.AmountHT = calc.AmountHT;
            line.AmountTVA = calc.AmountTVA;
            line.TaxSpecificAmount = calc.TaxSpecificAmount;
            line.AmountTTC = calc.AmountTTC;
        }

        // ═══════════════════════════════════════════════════════
        //  Pass 2 : OnTotal TS distribution
        // ═══════════════════════════════════════════════════════
        var onTotalGroups = invoice.Lines
            .Where(l => l.TaxApplicationMode == TaxApplicationMode.OnTotal
                      && l.SpecificTaxType != SpecificTaxType.None
                      && l.SpecificTaxValue > 0)
            .GroupBy(l => l.SpecificTaxName ?? $"__auto_{l.SpecificTaxType}_{l.SpecificTaxValue}");

        foreach (var grp in onTotalGroups)
        {
            var lines = grp.ToList();
            var representative = lines.First();

            if (representative.SpecificTaxType == SpecificTaxType.Percentage)
            {
                decimal tsRate = representative.SpecificTaxValue / 100m;

                foreach (var line in lines)
                {
                    decimal vatRate = line.TaxRate / 100m;

                    if (isTTC)
                    {
                        decimal goodsTTC = line.AmountTTC;
                        decimal goodsHT = line.AmountHT;
                        decimal tvaGoods = goodsTTC - goodsHT;

                        decimal ts = TaxCalculator.R2(goodsTTC * tsRate);
                        decimal tvaTS = TaxCalculator.R2(ts * vatRate);

                        line.TaxSpecificAmount = ts;
                        line.AmountHT = goodsHT + ts;
                        line.AmountTVA = tvaGoods + tvaTS;
                        line.AmountTTC = line.AmountHT + line.AmountTVA;
                    }
                    else
                    {
                        decimal goodsHT = line.AmountHT;
                        decimal ts = TaxCalculator.R2(goodsHT * tsRate);
                        decimal ht = goodsHT + ts;
                        decimal tva = TaxCalculator.R2(ht * vatRate);
                        decimal ttc = ht + tva;

                        line.TaxSpecificAmount = ts;
                        line.AmountHT = ht;
                        line.AmountTVA = tva;
                        line.AmountTTC = ttc;
                    }

                    if (line.AmountHT + line.AmountTVA != line.AmountTTC)
                        line.AmountTVA = line.AmountTTC - line.AmountHT;
                }
            }
            else if (representative.SpecificTaxType == SpecificTaxType.FixedPerUnit)
            {
                decimal groupQty = lines.Sum(l => l.Quantity);
                decimal tsForGroup = TaxCalculator.R2(groupQty * representative.SpecificTaxValue);

                decimal distributionBase = isTTC
                    ? lines.Sum(l => l.AmountTTC)
                    : lines.Sum(l => l.AmountHT);

                decimal distributed = 0m;

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    decimal share;

                    if (i < lines.Count - 1)
                    {
                        decimal lineBase = isTTC ? line.AmountTTC : line.AmountHT;
                        share = distributionBase > 0
                            ? TaxCalculator.R2(tsForGroup * lineBase / distributionBase)
                            : TaxCalculator.R2(tsForGroup / lines.Count);
                        distributed += share;
                    }
                    else
                    {
                        share = tsForGroup - distributed;
                    }

                    decimal vatRate = line.TaxRate / 100m;
                    line.TaxSpecificAmount = share;

                    if (isTTC)
                    {
                        decimal goodsTTC = line.AmountTTC;
                        decimal goodsHT = line.AmountHT;
                        decimal tvaGoods = goodsTTC - goodsHT;
                        decimal tvaTS = TaxCalculator.R2(share * vatRate);

                        line.AmountHT = goodsHT + share;
                        line.AmountTVA = tvaGoods + tvaTS;
                        line.AmountTTC = line.AmountHT + line.AmountTVA;
                    }
                    else
                    {
                        decimal goodsHT = line.AmountHT;
                        decimal newBase = goodsHT + share;
                        decimal newTTC = TaxCalculator.R2(newBase * (1m + vatRate));
                        decimal newTVA = newTTC - newBase;

                        line.AmountHT = newBase;
                        line.AmountTVA = newTVA;
                        line.AmountTTC = newTTC;
                    }

                    if (line.AmountHT + line.AmountTVA != line.AmountTTC)
                        line.AmountTVA = line.AmountTTC - line.AmountHT;
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Pass 3 (NEW V14): Group-level DGI rounding alignment
        //
        //  The fiscal device computes TVA per TAX GROUP (not per line):
        //    TTC mode: groupHT = R2(groupTTC / (1+rate)), groupTVA = groupTTC - groupHT
        //    HT mode:  groupTVA = R2(groupHT × rate), groupTTC = groupHT + groupTVA
        //
        //  Per-line rounding can accumulate a ±0.01 difference vs group-level.
        //  We adjust the last line in each group to absorb this difference.
        // ═══════════════════════════════════════════════════════
        var taxGroups = invoice.Lines
            .Where(l => l.TaxGroup != TaxGroup.N && l.TaxRate > 0)
            .GroupBy(l => l.TaxGroup);

        foreach (var group in taxGroups)
        {
            var groupLines = group.OrderBy(l => l.LineNumber).ToList();
            if (groupLines.Count == 0) continue;

            decimal rate = groupLines.First().TaxRate / 100m;

            if (isTTC)
            {
                // Device: groupHT = R2(groupTTC / (1+rate)), groupTVA = groupTTC - groupHT
                decimal groupTTC = groupLines.Sum(l => l.AmountTTC);
                decimal expectedHT = TaxCalculator.R2(groupTTC / (1m + rate));
                decimal expectedTVA = groupTTC - expectedHT;
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountHT -= diff; // TTC stays fixed
                }
            }
            else
            {
                // Device: groupTVA = R2(groupHT × rate), groupTTC = groupHT + groupTVA
                decimal groupHT = groupLines.Sum(l => l.AmountHT);
                decimal expectedTVA = TaxCalculator.R2(groupHT * rate);
                decimal expectedTTC = groupHT + expectedTVA;
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountTTC += diff; // HT stays fixed
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Pass 4 : Totaux
        // ═══════════════════════════════════════════════════════
        invoice.TotalHTBeforeDiscount = invoice.Lines.Sum(l => l.AmountHTBeforeDiscount);
        invoice.TotalDiscount = invoice.Lines.Sum(l => l.DiscountAmount);
        invoice.TotalHT = invoice.Lines.Sum(l => l.AmountHT);
        invoice.TotalTVA = invoice.Lines.Sum(l => l.AmountTVA);
        invoice.TotalTTC = invoice.Lines.Sum(l => l.AmountTTC);
        invoice.TotalSpecificTax = invoice.Lines.Sum(l => l.TaxSpecificAmount);

        invoice.TotalFixedSpecificTax = invoice.Lines
            .Where(l => l.SpecificTaxType == SpecificTaxType.FixedPerUnit)
            .Sum(l => l.TaxSpecificAmount);

        invoice.TotalPercentSpecificTax = invoice.Lines
            .Where(l => l.SpecificTaxType == SpecificTaxType.Percentage)
            .Sum(l => l.TaxSpecificAmount);
    }

    // ══════════════════════════════════════════════════════════
    //  VALIDATION — Async (credit note lookup)
    // ══════════════════════════════════════════════════════════

    private async Task<ValidationResult> ValidateInvoiceAsync(Invoice invoice)
    {
        if (invoice.Lines.Count == 0)
            return new("La facture doit contenir au moins un article.");

        if (invoice.TotalTTC <= 0)
            return new("Le montant total TTC doit être positif.");

        foreach (var line in invoice.Lines)
        {
            if (line.AmountTTC < 0)
                return new($"L'article « {line.Name} » a un montant TTC négatif.");

            if (line.AmountTTC == 0 && line.TaxGroup != TaxGroup.N)
                return new($"L'article « {line.Name} » a un montant TTC nul.");

            if ((line.TaxGroup == TaxGroup.L || line.TaxGroup == TaxGroup.N)
                && line.ItemType != ItemType.TAX)
                return new($"L'article « {line.Name} » dans le groupe {line.TaxGroup} doit être de type TAX.");

            if (line.DiscountType == DiscountType.Percentage && line.DiscountValue > 100)
                return new($"L'article « {line.Name} » : remise > 100 % non autorisée.");

            if (line.SpecificTaxType != SpecificTaxType.None)
            {
                if (line.SpecificTaxValue <= 0)
                    return new($"L'article « {line.Name} » : la valeur de la T.S. doit être > 0.");
                if (line.SpecificTaxType == SpecificTaxType.Percentage && line.SpecificTaxValue > 100)
                    return new($"L'article « {line.Name} » : taux T.S. > 100 % non autorisé.");
                if (line.TaxApplicationMode == TaxApplicationMode.OnTotal
                    && string.IsNullOrWhiteSpace(line.SpecificTaxName))
                    return new($"L'article « {line.Name} » : le nom de la T.S. est requis en mode « Sur total ».");
            }
        }

        var onTotalGroups = invoice.Lines
            .Where(l => l.SpecificTaxType != SpecificTaxType.None
                        && l.TaxApplicationMode == TaxApplicationMode.OnTotal
                        && !string.IsNullOrWhiteSpace(l.SpecificTaxName))
            .GroupBy(l => l.SpecificTaxName);

        foreach (var group in onTotalGroups)
        {
            var distinctTypes = group.Select(l => l.SpecificTaxType).Distinct().ToList();
            var distinctValues = group.Select(l => l.SpecificTaxValue).Distinct().ToList();
            if (distinctTypes.Count > 1 || distinctValues.Count > 1)
                return new($"T.S. « {group.Key} » incohérente : type/valeur doivent être identiques.");
        }

        switch (invoice.ClientType)
        {
            case ClientType.PM:
                if (string.IsNullOrWhiteSpace(invoice.ClientName))
                    return new("La dénomination est obligatoire pour une Personne Morale (PM).");
                if (string.IsNullOrWhiteSpace(invoice.ClientNIF))
                    return new("Le NIF est obligatoire pour une Personne Morale (PM).");
                break;
            case ClientType.PC:
                if (string.IsNullOrWhiteSpace(invoice.ClientName))
                    return new("Le nom est obligatoire pour une Personne physique commerçante (PC).");
                if (string.IsNullOrWhiteSpace(invoice.ClientNIF))
                    return new("Le NIF est obligatoire pour une Personne physique commerçante (PC).");
                break;
            case ClientType.PL:
                if (string.IsNullOrWhiteSpace(invoice.ClientName))
                    return new("Le nom est obligatoire pour une Profession libérale (PL).");
                if (string.IsNullOrWhiteSpace(invoice.ClientNIF))
                    return new("Le NIF est obligatoire pour une Profession libérale (PL).");
                break;
            case ClientType.AO:
                if (string.IsNullOrWhiteSpace(invoice.ClientName))
                    return new("Le nom est obligatoire pour les Ambassades / Organisations internationales (AO).");
                if (string.IsNullOrWhiteSpace(invoice.CommentA))
                    return new("La référence du certificat d'exonération (Commentaire Ligne A) est obligatoire pour AO.");
                break;
        }

        if (invoice.Lines.Any(l => l.TaxGroup == TaxGroup.D)
            && string.IsNullOrWhiteSpace(invoice.CommentA))
            return new("La référence du document de dérogation DGI (Ligne A) est obligatoire pour le groupe D.");

        if (invoice.Type.IsExport())
        {
            var nonExportLines = invoice.Lines
                .Where(l => l.TaxGroup != TaxGroup.E && l.TaxGroup != TaxGroup.L && l.TaxGroup != TaxGroup.N)
                .ToList();
            if (nonExportLines.Any())
                return new($"Facture d'exportation : les articles doivent être dans le groupe E (Exportation). " +
                           $"Article « {nonExportLines.First().Name} » est dans le groupe {nonExportLines.First().TaxGroup}.");
        }

        if (invoice.Type == InvoiceType.FA || invoice.Type == InvoiceType.EA)
        {
            if (invoice.CreditNoteNature == null)
                return new("La nature de la facture d'avoir est obligatoire.");

            if (string.IsNullOrWhiteSpace(invoice.OriginalInvoiceReference))
                return new("La référence de la facture originale est obligatoire.");

            if (invoice.CreditNoteNature == Domain.Enums.CreditNoteNature.RRR)
            {
                if (invoice.OriginalInvoiceReference.Trim().ToUpper() != "RRR")
                    return new("Pour une facture d'avoir de type RRR (Rabais/Remise/Ristourne), " +
                               "la référence de la facture originale doit être « RRR ».");
            }
            else
            {
                var originalInvoice = await _unitOfWork.Invoices.GetByCodeDEFDGIAsync(
                    invoice.OriginalInvoiceReference.Trim());

                if (originalInvoice == null)
                    return new($"Facture originale introuvable pour le Code DEF/DGI « {invoice.OriginalInvoiceReference} ».");

                if (originalInvoice.Status != InvoiceStatus.Normalized)
                    return new($"La facture originale « {invoice.OriginalInvoiceReference} » n'est pas normalisée.");

                if (originalInvoice.Type.IsCreditNote())
                    return new("Impossible de créer une facture d'avoir sur une autre facture d'avoir.");

                if (invoice.Type == InvoiceType.EA && !originalInvoice.Type.IsExport())
                    return new("Une facture d'avoir à l'exportation (EA) doit référencer une facture d'exportation.");
                if (invoice.Type == InvoiceType.FA && originalInvoice.Type.IsExport())
                    return new("Utilisez le type EA pour les avoirs sur factures d'exportation.");

                var cumulativeRefunded = await GetCumulativeRefundedQuantitiesAsync(
                    invoice.OriginalInvoiceReference.Trim());

                foreach (var line in invoice.Lines)
                {
                    var originalLine = originalInvoice.Lines.FirstOrDefault(
                        ol => ol.Code.Equals(line.Code, StringComparison.OrdinalIgnoreCase));

                    if (originalLine == null)
                        return new($"L'article « {line.Name} » (code: {line.Code}) n'existe pas " +
                                   $"sur la facture originale {invoice.OriginalInvoiceReference}.");

                    if (line.Quantity > originalLine.Quantity)
                        return new($"L'article « {line.Name} » : quantité à rembourser ({line.Quantity:G}) " +
                                   $"dépasse la quantité originale ({originalLine.Quantity:G}).");

                    decimal alreadyRefunded = cumulativeRefunded.GetValueOrDefault(line.Code, 0m);
                    if (alreadyRefunded + line.Quantity > originalLine.Quantity)
                        return new($"L'article « {line.Name} » : cumul des remboursements " +
                                   $"({alreadyRefunded:G} + {line.Quantity:G} = {alreadyRefunded + line.Quantity:G}) " +
                                   $"dépasse la quantité originale ({originalLine.Quantity:G}).");

                    if (invoice.PriceMode == PriceMode.HT)
                    {
                        if (Math.Abs(line.UnitPriceHT - originalLine.UnitPriceHT) > 0.01m)
                            return new($"L'article « {line.Name} » : le prix unitaire HT ({line.UnitPriceHT:F2}) " +
                                       $"doit être identique à celui de la facture originale ({originalLine.UnitPriceHT:F2}).");
                    }
                    else
                    {
                        if (Math.Abs(line.UnitPriceTTC - originalLine.UnitPriceTTC) > 0.01m)
                            return new($"L'article « {line.Name} » : le prix unitaire TTC ({line.UnitPriceTTC:F2}) " +
                                       $"doit être identique à celui de la facture originale ({originalLine.UnitPriceTTC:F2}).");
                    }
                }

                invoice.OriginalInvoiceId = originalInvoice.Id;
            }
        }

        if (invoice.Type == InvoiceType.FT || invoice.Type == InvoiceType.ET)
        {
            if (invoice.Payments.Count == 0)
                return new("Une facture d'acompte doit contenir au moins un paiement.");
        }

        decimal totalPaid = invoice.Payments.Sum(p => p.Amount);
        if (totalPaid < invoice.TotalTTC)
            return new($"Le total des paiements ({totalPaid:N2}) est inférieur au total TTC ({invoice.TotalTTC:N2}).");

        return new() { IsValid = true };
    }

    // ══════════════════════════════════════════════════════════
    //  CONSTRUCTION REQUÊTE FISCALE
    // ══════════════════════════════════════════════════════════

    private FiscalInvoiceRequest BuildFiscalRequest(Invoice invoice, Company company)
    {
        var request = new FiscalInvoiceRequest
        {
            NIF = company.NIF,
            InvoiceNumber = invoice.InvoiceNumber,
            PriceMode = invoice.PriceMode == PriceMode.TTC ? "TTC" : "HT",
            ISF = invoice.ISF,
            InvoiceType = invoice.Type.ToString(),
            OperatorId = invoice.OperatorId,
            OperatorName = invoice.OperatorName,
            CurrencyCode = string.IsNullOrEmpty(invoice.CurrencyCode)
                ? "CDF" : invoice.CurrencyCode,
            CurrencyRate = invoice.CurrencyRate,
            CurrencyDate = invoice.CurrencyDate ?? DateTime.Now,
            CommentA = invoice.CommentA ?? "",
            CommentB = invoice.CommentB ?? "",
            CommentC = invoice.CommentC ?? "",
            CommentD = invoice.CommentD ?? "",
            CommentE = invoice.CommentE ?? "",
            CommentF = invoice.CommentF ?? "",
            CommentG = invoice.CommentG ?? "",
            CommentH = invoice.CommentH ?? ""
        };

        if (invoice.Type == InvoiceType.FA || invoice.Type == InvoiceType.EA)
        {
            request.Reference = invoice.OriginalInvoiceReference ?? "";
            if (invoice.CreditNoteNature.HasValue)
            {
                request.ReferenceType = invoice.CreditNoteNature.Value.ToString();
                request.ReferenceDesc = GetCreditNoteNatureDesc(invoice.CreditNoteNature.Value);
            }
            else
            {
                request.ReferenceType = invoice.ReferenceType ?? "";
                request.ReferenceDesc = invoice.ReferenceDesc ?? "";
            }
        }
        else
        {
            request.Reference = "";
            request.ReferenceType = "";
            request.ReferenceDesc = "";
        }

        request.Client = new FiscalClientInfo
        {
            Type = invoice.ClientType.ToString(),
            TypeDesc = GetClientTypeDesc(invoice.ClientType),
            NIF = invoice.ClientNIF ?? "",
            Name = string.IsNullOrWhiteSpace(invoice.ClientName)
                ? invoice.ClientType.ToString()
                : invoice.ClientName,
            Address = invoice.ClientAddress ?? "",
            Contact = string.Join(" ", new[] { invoice.ClientPhone, invoice.ClientEmail }
                .Where(s => !string.IsNullOrEmpty(s)))
        };

        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            decimal baseUnitPrice = invoice.PriceMode == PriceMode.TTC
                ? line.UnitPriceTTC : line.UnitPriceHT;

            decimal rawPrice = baseUnitPrice;
            decimal effectivePrice = baseUnitPrice;
            string priceModification = "";

            if (line.DiscountType != DiscountType.None && line.DiscountAmount > 0)
            {
                if (line.DiscountType == DiscountType.Percentage)
                {
                    effectivePrice = Math.Round(
                        baseUnitPrice * (1m - line.DiscountValue / 100m), 2);
                    priceModification = $"Remise {line.DiscountValue}%";
                }
                else
                {
                    effectivePrice = line.Quantity != 0
                        ? Math.Round(
                            baseUnitPrice - (line.DiscountAmount / line.Quantity), 2)
                        : baseUnitPrice;
                    priceModification = $"Remise {line.DiscountAmount:F0}";
                }
            }

            string? fiscalTsValue = FormatSpecificTaxForDevice(
                line.SpecificTaxType, line.SpecificTaxValue);

            request.Items.Add(new FiscalItemInfo
            {
                Code = line.Code ?? "",
                Name = line.Name ?? "",
                Type = line.ItemType.ToString(),
                TaxGroup = ((char)('A' + (int)line.TaxGroup)).ToString(),
                TaxRate = line.TaxRate,
                Price = effectivePrice,
                Quantity = line.Quantity,
                TaxSpecificValue = fiscalTsValue,
                TaxSpecificAmount = line.TaxSpecificAmount,
                OriginalPrice = rawPrice,
                PriceModification = priceModification
            });
        }

        foreach (var payment in invoice.Payments)
        {
            string paymentCurrency = string.IsNullOrEmpty(payment.CurrencyCode)
                ? (string.IsNullOrEmpty(invoice.CurrencyCode) ? "CDF" : invoice.CurrencyCode)
                : payment.CurrencyCode;

            decimal paymentRate = payment.CurrencyRate > 0
                ? payment.CurrencyRate
                : invoice.CurrencyRate;

            request.Payments.Add(new FiscalPaymentInfo
            {
                Name = GetPaymentName(payment.PaymentType),
                Amount = payment.Amount,
                CurrencyCode = paymentCurrency,
                CurrencyRate = paymentRate
            });
        }

        return request;
    }

    private static string GetCreditNoteNatureDesc(CreditNoteNature nature) => nature switch
    {
        CreditNoteNature.COR => "Correction",
        CreditNoteNature.RAN => "Annulation",
        CreditNoteNature.RAM => "Avoir suite reprise",
        CreditNoteNature.RRR => "RRR",
        _ => ""
    };

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private static string? FormatSpecificTaxForDevice(SpecificTaxType type, decimal value) => type switch
    {
        SpecificTaxType.FixedPerUnit => value.ToString("G"),
        SpecificTaxType.Percentage => $"{value:G}%",
        _ => null
    };

    private static string GetClientTypeDesc(ClientType type) => type switch
    {
        ClientType.PP => "Personne physique",
        ClientType.PM => "Personne morale",
        ClientType.PC => "Personne physique commerçante",
        ClientType.PL => "Profession libérale",
        ClientType.AO => "Ambassade ou organisation internationale",
        _ => ""
    };

    private static string GetPaymentName(PaymentType type) => type switch
    {
        PaymentType.Especes => "ESPECES",
        PaymentType.Virement => "VIREMENT",
        PaymentType.CarteBancaire => "CARTEBANCAIRE",
        PaymentType.MobileMoney => "MOBILEMONEY",
        PaymentType.Cheques => "CHEQUES",
        PaymentType.Credit => "CREDIT",
        PaymentType.Autre => "AUTRE",
        _ => "ESPECES"
    };

    private async Task ApplyStockMovementsAsync(Invoice invoice)
    {
        bool isCreditNote = invoice.Type == InvoiceType.FA || invoice.Type == InvoiceType.EA;

        foreach (var line in invoice.Lines)
        {
            int? productId = line.ProductId;

            if (productId == null && !string.IsNullOrWhiteSpace(line.Code))
            {
                var product = await _unitOfWork.Products.GetByCodeAsync(line.Code);
                productId = product?.Id;
            }

            if (productId == null || productId <= 0)
                continue;

            if (isCreditNote)
            {
                await _stockService.IncrementForCreditNoteAsync(
                    productId.Value, invoice.PointOfSaleId, line.Quantity,
                    invoice.InvoiceNumber, invoice.OperatorName);
            }
            else
            {
                var result = await _stockService.DecrementForSaleAsync(
                    productId.Value, invoice.PointOfSaleId, line.Quantity,
                    invoice.InvoiceNumber, invoice.OperatorName);

                if (!result.Success && string.IsNullOrEmpty(invoice.CommentH))
                    invoice.CommentH = $"⚠ Stock: {result.ErrorMessage}";
            }
        }
    }
}

// ══════════════════════════════════════════════════════════
//  DTOs
// ══════════════════════════════════════════════════════════

public class NormalizationResult
{
    public bool Success { get; set; }
    public int InvoiceId { get; set; }
    public string CodeDEFDGI { get; set; } = "";
    public string QRCodeContent { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
    public ValidationResult() { }
    public ValidationResult(string error) { IsValid = false; ErrorMessage = error; }
}