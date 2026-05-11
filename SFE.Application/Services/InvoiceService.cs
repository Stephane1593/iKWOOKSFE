using System.Diagnostics;
using System.Text;
using SFE.Application.Helpers;
using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class InvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly StockService _stockService;
    private readonly IAuditService _auditService;
    private readonly ITimeProvider _time;

    public InvoiceService(
        IUnitOfWork unitOfWork,
        IFiscalDeviceService fiscalDevice,
        StockService stockService,
        IAuditService auditService,
        ITimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _fiscalDevice = fiscalDevice;
        _stockService = stockService;
        _auditService = auditService;
        _time = time;
    }

    public async Task<string> GenerateInvoiceNumberAsync(InvoiceType type, int pointOfSaleId)
    {
        return await _unitOfWork.Invoices
            .GenerateNextInvoiceNumberAsync(type, _time.UtcNow.Year, pointOfSaleId);
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
        return $"ADV-{_time.UtcNow.Year}/{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    // ══════════════════════════════════════════════════════════
    //  NORMALISATION — Flux complet (transactionnel)
    // ══════════════════════════════════════════════════════════

    public async Task<NormalizationResult> NormalizeInvoiceAsync(Invoice invoice)
    {
        // ── 1. Recalcul + validation ──
        RecalculateTotals(invoice);

        // 🆕 Pour FT/ET : on scale les lignes pour que TotalTTC = AdvanceAmount.
        if (invoice.IsAdvanceInvoice && invoice.AdvanceAmount > 0)
        {
            ApplyAdvanceScaling(invoice);
        }

        // 🆕 Auto-generate AdvanceGroupId if FT/ET and user didn't provide one
        if (invoice.IsAdvanceInvoice && string.IsNullOrWhiteSpace(invoice.AdvanceGroupId))
        {
            invoice.AdvanceGroupId = AdvanceGroupIdGenerator.Generate(_time);
        }

        var validation = await ValidateInvoiceAsync(invoice);
        if (!validation.IsValid)
        {
            await SafeAuditFailureAsync(
                AuditAction.InvoiceValidationFailed, invoice,
                $"Validation refusée: {validation.ErrorMessage}");

            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company == null)
        {
            await SafeAuditFailureAsync(
                AuditAction.InvoiceValidationFailed, invoice,
                "Entreprise non configurée.");

            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = "Entreprise non configurée."
            };
        }

        // ── 2. Soumission au dispositif fiscal ──
        var request = BuildFiscalRequest(invoice, company);
        invoice.Status = InvoiceStatus.Pending;

        var submitResult = await _fiscalDevice.SubmitInvoiceAsync(request);

        if (!submitResult.Success)
        {
            invoice.Status = InvoiceStatus.Error;
            var msg = $"Erreur dispositif fiscal [{submitResult.ErrorCode}]: {submitResult.ErrorMessage}";

            await SafeAuditFailureAsync(
                AuditAction.InvoiceFiscalDeviceError, invoice, msg);

            return new NormalizationResult { Success = false, ErrorMessage = msg };
        }

        invoice.EmcfUid = submitResult.Uid ?? "";

        // ── 3. Vérification cohérence des montants ──
        if (submitResult.TotalTTC > 0 &&
            (Math.Abs(submitResult.TotalTTC - invoice.TotalTTC) > 0.01m ||
             Math.Abs(submitResult.TotalTVA - invoice.TotalTVA) > 0.01m))
        {
            await SafeCancelFiscalAsync(invoice.EmcfUid);
            invoice.Status = InvoiceStatus.Error;

            var msg = $"Divergence montants — SFE TTC:{invoice.TotalTTC:F2} vs Dispositif:{submitResult.TotalTTC:F2}, " +
                      $"SFE TVA:{invoice.TotalTVA:F2} vs Dispositif:{submitResult.TotalTVA:F2}";

            await SafeAuditFailureAsync(
                AuditAction.InvoiceFiscalDeviceError, invoice, msg);

            return new NormalizationResult { Success = false, ErrorMessage = msg };
        }

        // ── 4. Finalisation côté dispositif ──
        var finalizeResult = await _fiscalDevice.FinalizeInvoiceAsync(
            invoice.EmcfUid, invoice.TotalTTC, invoice.TotalTVA);

        if (!finalizeResult.Success)
        {
            invoice.Status = InvoiceStatus.Error;
            var msg = $"Erreur finalisation [{finalizeResult.ErrorCode}]: {finalizeResult.ErrorMessage}";

            await SafeAuditFailureAsync(
                AuditAction.InvoiceFiscalDeviceError, invoice, msg);

            return new NormalizationResult { Success = false, ErrorMessage = msg };
        }

        invoice.CodeDEFDGI = finalizeResult.CodeDEFDGI ?? "";
        invoice.QRCodeContent = finalizeResult.QRCode ?? "";
        invoice.NIM = finalizeResult.NIM ?? "";
        invoice.Counters = finalizeResult.Counters ?? "";
        invoice.DeviceDateTime = finalizeResult.DateTime ?? "";
        invoice.Status = InvoiceStatus.Normalized;
        invoice.NormalizedAt = _time.UtcNow;           // ← DateTimeOffset

        // ⚠️ À PARTIR D'ICI : la facture est NORMALISÉE côté dispositif fiscal.

        // ══════════════════════════════════════════════════════════
        //  5. SAUVEGARDE DE LA FACTURE
        // ══════════════════════════════════════════════════════════
        try
        {
            if (invoice.CreatedAt == default)
                invoice.CreatedAt = _time.UtcNow;

            const int MAX_NUMBER_ATTEMPTS = 5;
            for (int attempt = 1; attempt <= MAX_NUMBER_ATTEMPTS; attempt++)
            {
                var clash = await _unitOfWork.Invoices.GetByInvoiceNumberAsync(invoice.InvoiceNumber);
                if (clash == null) break;

                invoice.InvoiceNumber = await _unitOfWork.Invoices
                    .GenerateNextInvoiceNumberAsync(invoice.Type, _time.UtcNow.Year, invoice.PointOfSaleId);
            }

            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await SafeCancelFiscalAsync(invoice.EmcfUid);

            var deepMessage = FlattenException(ex);
            Debug.WriteLine($"=== INVOICE SAVE FAILURE ===\n{deepMessage}\n============================");

            await SafeAuditFailureAsync(
                AuditAction.InvoiceSaveFailed,
                invoice,
                $"Échec sauvegarde après normalisation : {ex.GetBaseException().Message}",
                deepMessage);

            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Erreur lors de la sauvegarde : {ex.GetBaseException().Message}"
            };
        }

        // ══════════════════════════════════════════════════════════
        //  6. STOCK MOVEMENTS (best-effort)
        // ══════════════════════════════════════════════════════════
        try
        {
            await ApplyStockMovementsAsync(invoice);
        }
        catch (Exception stockEx)
        {
            var deepMessage = FlattenException(stockEx);
            Debug.WriteLine($"=== STOCK MOVEMENT FAILURE ===\n{deepMessage}\n==============================");

            try
            {
                if (string.IsNullOrEmpty(invoice.CommentH))
                    invoice.CommentH = $"⚠ Stock: {stockEx.GetBaseException().Message}";

                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                Debug.WriteLine($"[NormalizeInvoice] Could not save stock warning to invoice: {saveEx.Message}");
            }

            await SafeAuditFailureAsync(
                AuditAction.InvoiceNormalizationFailed,
                invoice,
                $"Facture normalisée mais erreur de mise à jour du stock : {stockEx.GetBaseException().Message}",
                deepMessage);
        }

        // ══════════════════════════════════════════════════════════
        //  7. AUDIT SUCCÈS
        // ══════════════════════════════════════════════════════════
        var auditAction = invoice.IsCreditNote
            ? AuditAction.CreditNoteNormalized
            : invoice.IsAdvanceInvoice
                ? AuditAction.AdvanceInvoiceNormalized
                : AuditAction.InvoiceNormalized;

        try
        {
            await _auditService.LogInvoiceAsync(auditAction, invoice);
        }
        catch (Exception auditEx)
        {
            Debug.WriteLine($"[NormalizeInvoice] Audit success log failed: {auditEx.Message}");
        }

        return new NormalizationResult
        {
            Success = true,
            InvoiceId = invoice.Id,
            CodeDEFDGI = invoice.CodeDEFDGI,
            QRCodeContent = invoice.QRCodeContent
        };
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS — Audit & cleanup d'échec (best-effort)
    // ══════════════════════════════════════════════════════════

    private async Task SafeAuditFailureAsync(
        AuditAction action, Invoice invoice, string description, string? details = null)
    {
        try
        {
            var detailJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                invoice.Type,
                invoice.InvoiceNumber,
                invoice.Status,
                invoice.EmcfUid,
                invoice.CodeDEFDGI,
                invoice.TotalTTC,
                invoice.OriginalInvoiceReference,
                invoice.CreditNoteNature,
                LinesCount = invoice.Lines?.Count ?? 0,
                ErrorDetails = details
            });

            await _auditService.LogAsync(
                action,
                AuditModule.Invoicing,
                description,
                entityType: "Invoice",
                entityId: invoice.Id > 0 ? invoice.Id.ToString() : invoice.InvoiceNumber,
                codeDEFDGI: invoice.CodeDEFDGI,
                invoiceNumber: invoice.InvoiceNumber,
                details: detailJson);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InvoiceService] Audit failure log failed: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  🆕 ACOMPTE — Scaling proportionnel des lignes
    // ══════════════════════════════════════════════════════════
    private void ApplyAdvanceScaling(Invoice invoice)
    {
        decimal orderTotal = invoice.TotalTTC;
        if (orderTotal <= 0) return;

        if (invoice.AdvanceAmount > orderTotal + 0.01m)
            throw new InvalidOperationException(
                $"L'acompte ({invoice.AdvanceAmount:N2}) dépasse le total commandé ({orderTotal:N2}).");

        invoice.OrderTotal = orderTotal;
        decimal factor = invoice.AdvanceAmount / orderTotal;

        foreach (var line in invoice.Lines)
        {
            line.UnitPriceHT = TaxCalculator.R2(line.UnitPriceHT * factor);
            line.UnitPriceTTC = TaxCalculator.R2(line.UnitPriceTTC * factor);

            if (line.DiscountType == DiscountType.FixedAmount)
                line.DiscountValue = TaxCalculator.R2(line.DiscountValue * factor);

            if (line.SpecificTaxType == SpecificTaxType.FixedPerUnit)
                line.SpecificTaxValue = TaxCalculator.R2(line.SpecificTaxValue * factor);
        }

        RecalculateTotals(invoice);

        decimal diff = invoice.AdvanceAmount - invoice.TotalTTC;
        if (diff != 0m && invoice.Lines.Count > 0)
        {
            var last = invoice.Lines.OrderBy(l => l.LineNumber).Last();
            last.AmountTTC += diff;
            decimal vatRate = last.TaxRate / 100m;
            if (vatRate > 0)
            {
                decimal newHT = TaxCalculator.R2(last.AmountTTC / (1m + vatRate));
                last.AmountTVA = last.AmountTTC - newHT;
                last.AmountHT = newHT;
            }
            else
            {
                last.AmountHT = last.AmountTTC;
                last.AmountTVA = 0m;
            }
            invoice.TotalHT = invoice.Lines.Sum(l => l.AmountHT);
            invoice.TotalTVA = invoice.Lines.Sum(l => l.AmountTVA);
            invoice.TotalTTC = invoice.Lines.Sum(l => l.AmountTTC);
        }

        invoice.RemainingAfterAdvance =
            invoice.OrderTotal - invoice.PreviousAdvancesTotal - invoice.AdvanceAmount;
    }

    private async Task SafeCancelFiscalAsync(string? emcfUid)
    {
        if (string.IsNullOrWhiteSpace(emcfUid)) return;

        try
        {
            await _fiscalDevice.CancelPendingInvoiceAsync(emcfUid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InvoiceService] CancelPendingInvoiceAsync failed for {emcfUid}: {ex.Message}");
        }
    }

    private static string FlattenException(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        int level = 0;
        while (current != null)
        {
            sb.AppendLine($"  [{level}] {current.GetType().Name}: {current.Message}");
            current = current.InnerException;
            level++;
        }
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  RECALCUL DES TOTAUX — V13
    // ══════════════════════════════════════════════════════════

    public void RecalculateTotals(Invoice invoice)
    {
        bool isTTC = invoice.PriceMode == PriceMode.TTC;

        // Pass 1
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

        // Pass 2 : OnTotal TS
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

        // Pass 3 : Group-level DGI rounding alignment
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
                decimal groupTTC = groupLines.Sum(l => l.AmountTTC);
                decimal expectedHT = TaxCalculator.R2(groupTTC / (1m + rate));
                decimal expectedTVA = groupTTC - expectedHT;
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountHT -= diff;
                }
            }
            else
            {
                decimal groupHT = groupLines.Sum(l => l.AmountHT);
                decimal expectedTVA = TaxCalculator.R2(groupHT * rate);
                decimal expectedTTC = groupHT + expectedTVA;
                decimal actualTVA = groupLines.Sum(l => l.AmountTVA);
                decimal diff = expectedTVA - actualTVA;

                if (diff != 0m)
                {
                    var lastLine = groupLines.Last();
                    lastLine.AmountTVA += diff;
                    lastLine.AmountTTC += diff;
                }
            }
        }

        // Pass 4 : Totaux
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
    //  VALIDATION
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
            // 🔧 Was: `invoice.CurrencyDate ?? DateTimeOffset` (broken). 
            //         CurrencyDate on the entity is DateTime?, so feed a DateTime.
            CurrencyDate = (invoice.CurrencyDate ?? _time.UtcNow).UtcDateTime,
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

    // ══════════════════════════════════════════════════════════
    //  STOCK
    // ══════════════════════════════════════════════════════════
    private async Task ApplyStockMovementsAsync(Invoice invoice)
    {
        foreach (var line in invoice.Lines)
        {
            if (line.ItemType == ItemType.TAX)
                continue;

            int? productId = line.ProductId;
            if (productId == null && !string.IsNullOrWhiteSpace(line.Code))
            {
                var product = await _unitOfWork.Products.GetByCodeAsync(line.Code);
                productId = product?.Id;
            }
            if (productId == null || productId <= 0)
                continue;

            var impact = DetermineStockImpact(invoice);

            switch (impact)
            {
                case StockImpactKind.Decrement:
                    var decResult = await _stockService.DecrementForSaleAsync(
                        productId.Value, invoice.PointOfSaleId, line.Quantity,
                        invoice.InvoiceNumber, invoice.OperatorName);

                    if (!decResult.Success && string.IsNullOrEmpty(invoice.CommentH))
                        invoice.CommentH = $"⚠ Stock: {decResult.ErrorMessage}";
                    break;

                case StockImpactKind.Increment:
                    await _stockService.IncrementForCreditNoteAsync(
                        productId.Value, invoice.PointOfSaleId, line.Quantity,
                        invoice.InvoiceNumber, invoice.OperatorName);
                    break;

                case StockImpactKind.None:
                    break;
            }
        }
    }

    private static StockImpactKind DetermineStockImpact(Invoice invoice)
    {
        if (invoice.Type == InvoiceType.FT || invoice.Type == InvoiceType.ET)
            return StockImpactKind.None;

        if (invoice.Type == InvoiceType.FV || invoice.Type == InvoiceType.EV)
            return StockImpactKind.Decrement;

        if (invoice.Type == InvoiceType.FA || invoice.Type == InvoiceType.EA)
        {
            return invoice.CreditNoteNature switch
            {
                CreditNoteNature.RAN => StockImpactKind.Increment,
                CreditNoteNature.RAM => StockImpactKind.Increment,
                CreditNoteNature.COR => StockImpactKind.Increment,
                CreditNoteNature.RRR => StockImpactKind.None,
                _ => StockImpactKind.None
            };
        }

        return StockImpactKind.None;
    }

    private enum StockImpactKind { None, Decrement, Increment }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Numérotation
    // ══════════════════════════════════════════════════════════

    public async Task<string> GenerateProformaNumberAsync(int pointOfSaleId)
    {
        return await _unitOfWork.Invoices
            .GenerateNextProformaNumberAsync(_time.UtcNow.Year, pointOfSaleId);
    }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Sauvegarde
    // ══════════════════════════════════════════════════════════

    public async Task<NormalizationResult> SaveProformaAsync(Invoice proforma)
    {
        if (proforma.Type != InvoiceType.PRO)
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = "Le type doit être PRO."
            };

        RecalculateTotals(proforma);

        var validation = ValidateProforma(proforma, _time.UtcToday);
        if (!validation.IsValid)
        {
            await SafeAuditFailureAsync(
                AuditAction.InvoiceValidationFailed, proforma,
                $"Proforma refusée : {validation.ErrorMessage}");

            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        proforma.Status = InvoiceStatus.Draft;

        if (proforma.CreatedAt == default)
            proforma.CreatedAt = _time.UtcNow;

        proforma.CodeDEFDGI = "";
        proforma.QRCodeContent = "";
        proforma.NIM = "";
        proforma.Counters = "";
        proforma.DeviceDateTime = "";
        proforma.EmcfUid = "";
        proforma.NormalizedAt = null;

        try
        {
            const int MAX_ATTEMPTS = 5;
            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                var clash = await _unitOfWork.Invoices.GetByInvoiceNumberAsync(proforma.InvoiceNumber);
                if (clash == null) break;
                proforma.InvoiceNumber = await GenerateProformaNumberAsync(proforma.PointOfSaleId);
            }

            await _unitOfWork.Invoices.AddAsync(proforma);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var deep = FlattenException(ex);
            Debug.WriteLine($"=== PROFORMA SAVE FAILURE ===\n{deep}");

            await SafeAuditFailureAsync(
                AuditAction.InvoiceSaveFailed, proforma,
                $"Échec sauvegarde proforma : {ex.GetBaseException().Message}",
                deep);

            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Erreur sauvegarde : {ex.GetBaseException().Message}"
            };
        }

        try
        {
            await _auditService.LogInvoiceAsync(AuditAction.ProformaCreated, proforma);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveProforma] Audit failed: {ex.Message}");
        }

        return new NormalizationResult
        {
            Success = true,
            InvoiceId = proforma.Id,
            CodeDEFDGI = "",
            QRCodeContent = ""
        };
    }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Conversion
    // ══════════════════════════════════════════════════════════

    public async Task<NormalizationResult> ConvertProformaAsync(
        int proformaId,
        InvoiceType targetType,
        string operatorName,
        string operatorId,
        decimal advanceAmount = 0m)
    {
        var pro = await _unitOfWork.Invoices.GetWithDetailsAsync(proformaId);

        if (pro == null)
            return new NormalizationResult { Success = false, ErrorMessage = "Proforma introuvable." };

        if (pro.Type != InvoiceType.PRO)
            return new NormalizationResult { Success = false, ErrorMessage = "Le document source n'est pas une proforma." };

        if (pro.ConvertedToInvoiceId.HasValue)
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Cette proforma a déjà été convertie (facture #{pro.ConvertedToInvoiceId})."
            };

        if (pro.Status == InvoiceStatus.Cancelled)
            return new NormalizationResult { Success = false, ErrorMessage = "Cette proforma a été annulée." };

        // ProformaValidUntil is DateTime? — compare through DateOnly using the time provider.
        if (pro.ProformaValidUntil.HasValue
            && DateOnly.FromDateTime(pro.ProformaValidUntil.Value.UtcDateTime) < _time.UtcToday)
        {
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = $"Proforma expirée le {pro.ProformaValidUntil:dd/MM/yyyy}."
            };
        }

        if (targetType == InvoiceType.PRO || targetType.IsCreditNote())
            return new NormalizationResult
            {
                Success = false,
                ErrorMessage = "Type cible invalide. Une proforma se convertit en FV, EV, FT ou ET."
            };

        var newNumber = await _unitOfWork.Invoices
            .GenerateNextInvoiceNumberAsync(targetType, _time.UtcNow.Year, pro.PointOfSaleId);

        var nowUtc = _time.UtcNow;

        var fiscal = new Invoice
        {
            InvoiceNumber = newNumber,
            Type = targetType,
            Status = InvoiceStatus.Draft,
            PriceMode = pro.PriceMode,
            DiscountBeforeTax = pro.DiscountBeforeTax,
            ISF = pro.ISF,

            ClientType = pro.ClientType,
            ClientNIF = pro.ClientNIF,
            ClientName = pro.ClientName,
            ClientAddress = pro.ClientAddress,
            ClientPhone = pro.ClientPhone,
            ClientEmail = pro.ClientEmail,
            ClientRCCM = pro.ClientRCCM,

            OperatorName = operatorName,
            OperatorId = operatorId,

            CurrencyCode = pro.CurrencyCode,
            CurrencyRate = pro.CurrencyRate,
            CreatedAt = nowUtc,
            CurrencyDate = nowUtc.UtcDateTime,   // CurrencyDate is DateTime?

            CommentA = pro.CommentA,
            CommentB = pro.CommentB,
            CommentC = pro.CommentC,
            CommentD = pro.CommentD,
            CommentE = pro.CommentE,
            CommentF = pro.CommentF,
            CommentG = pro.CommentG,
            CommentH = string.IsNullOrWhiteSpace(pro.CommentH)
                ? $"Issue de la proforma {pro.InvoiceNumber} du {pro.CreatedAt:dd/MM/yyyy}"
                : pro.CommentH,

            SourceProformaId = pro.Id,
            PointOfSaleId = pro.PointOfSaleId,
        };

        if (targetType is InvoiceType.FT or InvoiceType.ET)
        {
            if (advanceAmount <= 0)
                return new NormalizationResult
                {
                    Success = false,
                    ErrorMessage = "Le montant d'acompte est obligatoire pour FT/ET."
                };
            fiscal.AdvanceAmount = advanceAmount;
            fiscal.AdvanceGroupId = AdvanceGroupIdGenerator.Generate(_time);
        }

        foreach (var pl in pro.Lines.OrderBy(l => l.LineNumber))
        {
            fiscal.Lines.Add(new InvoiceLine
            {
                LineNumber = pl.LineNumber,
                ProductId = pl.ProductId,
                Code = pl.Code,
                Name = pl.Name,
                ItemType = pl.ItemType,
                TaxGroup = pl.TaxGroup,
                TaxRate = pl.TaxRate,
                UnitPriceHT = pl.UnitPriceHT,
                UnitPriceTTC = pl.UnitPriceTTC,
                Quantity = pl.Quantity,
                Unit = pl.Unit,
                DiscountType = pl.DiscountType,
                DiscountValue = pl.DiscountValue,
                SpecificTaxName = pl.SpecificTaxName,
                SpecificTaxType = pl.SpecificTaxType,
                SpecificTaxValue = pl.SpecificTaxValue,
                TaxApplicationMode = pl.TaxApplicationMode,
            });
        }

        var result = await NormalizeInvoiceAsync(fiscal);

        if (result.Success)
        {
            pro.ConvertedToInvoiceId = result.InvoiceId;
            pro.Status = InvoiceStatus.Converted;
            pro.UpdatedAt = nowUtc;           // DateTimeOffset

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConvertProforma] Failed to mark proforma as converted: {ex.Message}");
            }

            try
            {
                await _auditService.LogAsync(
                    AuditAction.ProformaConverted,
                    AuditModule.Invoicing,
                    $"Proforma {pro.InvoiceNumber} convertie en {newNumber} ({targetType.Label()})",
                    entityType: "Invoice",
                    entityId: pro.Id.ToString(),
                    codeDEFDGI: result.CodeDEFDGI,
                    invoiceNumber: newNumber,
                    details: $"{{\"sourceProformaId\":{pro.Id},\"targetInvoiceId\":{result.InvoiceId},\"targetType\":\"{targetType}\"}}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConvertProforma] Audit failed: {ex.Message}");
            }
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Annulation
    // ══════════════════════════════════════════════════════════

    public async Task<bool> CancelProformaAsync(int proformaId, string reason)
    {
        var pro = await _unitOfWork.Invoices.GetWithDetailsAsync(proformaId);
        if (pro == null || pro.Type != InvoiceType.PRO) return false;
        if (pro.ConvertedToInvoiceId.HasValue) return false;
        if (pro.Status == InvoiceStatus.Cancelled) return true;

        pro.Status = InvoiceStatus.Cancelled;
        pro.UpdatedAt = _time.UtcNow;           // DateTimeOffset
        pro.CommentH = string.IsNullOrEmpty(pro.CommentH)
            ? $"Annulation : {reason}"
            : $"{pro.CommentH} | Annulation : {reason}";

        await _unitOfWork.SaveChangesAsync();

        try
        {
            await _auditService.LogAsync(
                AuditAction.ProformaCancelled,
                AuditModule.Invoicing,
                $"Proforma {pro.InvoiceNumber} annulée — {reason}",
                entityType: "Invoice",
                entityId: pro.Id.ToString(),
                invoiceNumber: pro.InvoiceNumber);
        }
        catch { }

        return true;
    }

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Liste active
    // ══════════════════════════════════════════════════════════

    public async Task<List<Invoice>> GetActiveProformasAsync(int? pointOfSaleId = null)
        => await _unitOfWork.Invoices.GetActiveProformasAsync(pointOfSaleId, excludeExpired: true);

    // ══════════════════════════════════════════════════════════
    //  PROFORMA — Validation allégée
    //  Now takes "today" explicitly so the method stays static.
    // ══════════════════════════════════════════════════════════

    private static ValidationResult ValidateProforma(Invoice pro, DateOnly today)
    {
        if (pro.Lines.Count == 0)
            return new("La proforma doit contenir au moins un article.");

        if (pro.TotalTTC <= 0)
            return new("Le total TTC doit être strictement positif.");

        foreach (var line in pro.Lines)
        {
            if (line.AmountTTC < 0)
                return new($"L'article « {line.Name} » a un montant TTC négatif.");

            if (line.DiscountType == DiscountType.Percentage && line.DiscountValue > 100)
                return new($"L'article « {line.Name} » : remise > 100 % non autorisée.");

            if (line.SpecificTaxType != SpecificTaxType.None)
            {
                if (line.SpecificTaxValue <= 0)
                    return new($"L'article « {line.Name} » : valeur T.S. invalide.");
                if (line.SpecificTaxType == SpecificTaxType.Percentage && line.SpecificTaxValue > 100)
                    return new($"L'article « {line.Name} » : taux T.S. > 100 %.");
            }
        }

        if (pro.ProformaValidUntil.HasValue
            && DateOnly.FromDateTime(pro.ProformaValidUntil.Value.UtcDateTime) < today)
        {
            return new("La date de validité ne peut pas être dans le passé.");
        }

        return new() { IsValid = true };
    }

    // ══════════════════════════════════════════════════════════
    //  PRINT TRACKING
    // ══════════════════════════════════════════════════════════

    public async Task<int> RegisterPrintAsync(int invoiceId)
    {
        var inv = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        if (inv == null)
            throw new InvalidOperationException($"Facture #{invoiceId} introuvable.");

        var newCount = inv.PrintCount + 1;
        // FirstPrintedAt / LastPrintedAt are DateTime? on the entity — use UTC DateTime.
        var now = _time.UtcNow.UtcDateTime;

        inv.PrintCount = newCount;
        inv.LastPrintedAt = now;
        if (inv.FirstPrintedAt == null)
            inv.FirstPrintedAt = now;

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegisterPrint] Save failed: {ex.Message}");
            return Math.Max(1, newCount);
        }

        try
        {
            if (inv.IsProforma)
            {
                await _auditService.LogAsync(
                    AuditAction.InvoicePrinted,
                    AuditModule.Invoicing,
                    $"Proforma {inv.InvoiceNumber} imprimée (tirage #{newCount})",
                    entityType: "Invoice",
                    entityId: inv.Id.ToString(),
                    invoiceNumber: inv.InvoiceNumber);
            }
            else if (newCount == 1)
            {
                await _auditService.LogAsync(
                    AuditAction.InvoicePrinted,
                    AuditModule.Invoicing,
                    $"Original imprimé : {inv.InvoiceNumber}",
                    entityType: "Invoice",
                    entityId: inv.Id.ToString(),
                    invoiceNumber: inv.InvoiceNumber);
            }
            else
            {
                await _auditService.LogAsync(
                    AuditAction.InvoiceDuplicated,
                    AuditModule.Invoicing,
                    $"Duplicata N°{newCount - 1} émis : {inv.InvoiceNumber}",
                    entityType: "Invoice",
                    entityId: inv.Id.ToString(),
                    invoiceNumber: inv.InvoiceNumber,
                    details: $"{{\"printCount\":{newCount},\"duplicateNumber\":{newCount - 1}}}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegisterPrint] Audit failed: {ex.Message}");
        }

        return newCount;
    }

    public async Task<int> PeekPrintNumberAsync(int invoiceId)
    {
        var inv = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
        return inv == null ? 1 : inv.PrintCount + 1;
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