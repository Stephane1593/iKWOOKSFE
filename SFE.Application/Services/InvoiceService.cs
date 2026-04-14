using SFE.Application.Interfaces;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class InvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFiscalDeviceService _fiscalDevice;
    private readonly StockService _stockService;

    public InvoiceService(IUnitOfWork unitOfWork, IFiscalDeviceService fiscalDevice, StockService stockService)
    {
        _unitOfWork = unitOfWork;
        _fiscalDevice = fiscalDevice;
        _stockService = stockService;
    }

    public async Task<string> GenerateInvoiceNumberAsync(InvoiceType type)
    {
        return await _unitOfWork.Invoices.GenerateNextInvoiceNumberAsync(type, DateTime.Now.Year);
    }

    // ══════════════════════════════════════════════════════════
    //  🆕 LOOKUP FACTURE ORIGINALE (pour FA/EA)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Recherche la facture originale par Code DEF/DGI.
    /// Retourne null si introuvable ou non normalisée.
    /// </summary>
    public async Task<Invoice?> LookupOriginalInvoiceAsync(string codeDEFDGI)
    {
        if (string.IsNullOrWhiteSpace(codeDEFDGI))
            return null;

        var invoice = await _unitOfWork.Invoices.GetByCodeDEFDGIAsync(codeDEFDGI.Trim());

        if (invoice == null || invoice.Status != InvoiceStatus.Normalized)
            return null;

        // Only FV, FT, EV, ET can be referenced (not another credit note)
        if (invoice.Type.IsCreditNote())
            return null;

        return invoice;
    }

    // ══════════════════════════════════════════════════════════
    //  🆕 CUMUL DES QUANTITÉS DÉJÀ REMBOURSÉES (pour FA/EA)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Retourne un dictionnaire {articleCode → quantité cumulée déjà remboursée}
    /// pour toutes les FA/EA normalisées référençant la même facture originale.
    /// </summary>
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
    //  🆕 AVANCES — Récupérer les FT/ET d'un groupe
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Retourne toutes les factures d'acompte (FT/ET) normalisées d'un groupe.
    /// </summary>
    public async Task<List<Invoice>> GetAdvancesForGroupAsync(string advanceGroupId)
    {
        if (string.IsNullOrWhiteSpace(advanceGroupId))
            return new List<Invoice>();

        return await _unitOfWork.Invoices.GetAdvancesByGroupAsync(advanceGroupId);
    }

    /// <summary>
    /// Calcule le total des acomptes déjà versés pour un groupe.
    /// </summary>
    public async Task<decimal> GetTotalAdvancesPaidAsync(string advanceGroupId)
    {
        var advances = await GetAdvancesForGroupAsync(advanceGroupId);
        return advances.Sum(a => a.TotalTTC);
    }

    /// <summary>
    /// Génère un nouvel identifiant de groupe d'avances.
    /// </summary>
    public string GenerateAdvanceGroupId()
    {
        return $"ADV-{DateTime.Now.Year}/{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    // ══════════════════════════════════════════════════════════
    //  NORMALISATION — Flux complet (🐛 FIX stock)
    // ══════════════════════════════════════════════════════════

    public async Task<NormalizationResult> NormalizeInvoiceAsync(Invoice invoice)
    {
        // 1. Recalculer les totaux AVANT validation
        RecalculateTotals(invoice);

        // 2. Validation
        var validation = await ValidateInvoiceAsync(invoice); // 🆕 async for credit note lookup
        if (!validation.IsValid)
            return new NormalizationResult { Success = false, ErrorMessage = validation.ErrorMessage };

        // 3. Charger l'entreprise
        var company = await _unitOfWork.Companies.GetCurrentCompanyAsync();
        if (company == null)
            return new NormalizationResult { Success = false, ErrorMessage = "Entreprise non configurée." };

        // 4. Construire la requête fiscale
        var request = BuildFiscalRequest(invoice, company);

        // 5. Envoyer au dispositif fiscal
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

        // 6. Vérification des montants retournés
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

        // 7. Confirmer (finaliser)
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

        // 8. Enregistrer les éléments de sécurité
        invoice.CodeDEFDGI = finalizeResult.CodeDEFDGI ?? "";
        invoice.QRCodeContent = finalizeResult.QRCode ?? "";
        invoice.NIM = finalizeResult.NIM ?? "";
        invoice.Counters = finalizeResult.Counters ?? "";
        invoice.DeviceDateTime = finalizeResult.DateTime ?? "";
        invoice.Status = InvoiceStatus.Normalized;
        invoice.NormalizedAt = DateTime.Now;

        // 9. 🐛 FIX: Stock AVANT la sauvegarde (était après return)
        await ApplyStockMovementsAsync(invoice);

        // 10. Sauvegarder en base
        await _unitOfWork.Invoices.AddAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        return new NormalizationResult
        {
            Success = true,
            InvoiceId = invoice.Id,
            CodeDEFDGI = invoice.CodeDEFDGI,
            QRCodeContent = invoice.QRCodeContent
        };
    }

    // ══════════════════════════════════════════════════════════
    //  RECALCUL DES TOTAUX (unchanged — keeping as-is)
    // ══════════════════════════════════════════════════════════

    public void RecalculateTotals(Invoice invoice)
    {
        decimal totalHTBefore = 0, totalDiscount = 0;
        decimal totalHT = 0, totalTVA = 0, totalTTC = 0;
        decimal totalFixedTS = 0, totalPercentTS = 0;

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

            totalHTBefore += calc.AmountHTBeforeDiscount;
            totalDiscount += calc.DiscountAmount;
            totalHT += calc.AmountHT;
            totalTVA += calc.AmountTVA;
            totalTTC += calc.AmountTTC;

            if (line.TaxApplicationMode == TaxApplicationMode.PerArticle)
            {
                switch (line.SpecificTaxType)
                {
                    case SpecificTaxType.FixedPerUnit:
                        totalFixedTS += calc.TaxSpecificAmount;
                        break;
                    case SpecificTaxType.Percentage:
                        totalPercentTS += calc.TaxSpecificAmount;
                        break;
                }
            }
        }

        // Pass 2: OnTotal
        var onTotalGroups = invoice.Lines
            .Where(l => l.SpecificTaxType != SpecificTaxType.None
                        && l.TaxApplicationMode == TaxApplicationMode.OnTotal)
            .GroupBy(l => l.SpecificTaxName ?? $"__auto_{l.SpecificTaxType}_{l.SpecificTaxValue}");

        foreach (var group in onTotalGroups)
        {
            var representative = group.First();
            decimal onTotalAmount;

            switch (representative.SpecificTaxType)
            {
                case SpecificTaxType.FixedPerUnit:
                    {
                        decimal totalQuantity = group.Sum(l => l.Quantity);
                        onTotalAmount = Math.Round(totalQuantity * representative.SpecificTaxValue, 2);
                        totalFixedTS += onTotalAmount;
                        break;
                    }
                case SpecificTaxType.Percentage:
                    {
                        decimal groupHT = group.Sum(l => l.AmountHT);
                        onTotalAmount = Math.Round(groupHT * representative.SpecificTaxValue / 100m, 2);
                        totalPercentTS += onTotalAmount;
                        break;
                    }
                default:
                    onTotalAmount = 0;
                    break;
            }

            totalTTC += onTotalAmount;

            if (onTotalAmount > 0)
                DistributeOnTotalTaxToLines(group, representative.SpecificTaxType, onTotalAmount);
        }

        invoice.TotalHTBeforeDiscount = totalHTBefore;
        invoice.TotalDiscount = totalDiscount;
        invoice.TotalHT = totalHT;
        invoice.TotalTVA = totalTVA;
        invoice.TotalFixedSpecificTax = totalFixedTS;
        invoice.TotalPercentSpecificTax = totalPercentTS;
        invoice.TotalSpecificTax = totalFixedTS + totalPercentTS;
        invoice.TotalTTC = totalTTC;
    }

    private static void DistributeOnTotalTaxToLines(
        IGrouping<string, InvoiceLine> group,
        SpecificTaxType taxType,
        decimal totalAmount)
    {
        var lines = group.ToList();
        decimal distributionBase = taxType == SpecificTaxType.FixedPerUnit
            ? lines.Sum(l => l.Quantity)
            : lines.Sum(l => l.AmountHT);

        if (distributionBase == 0) return;

        decimal distributed = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            decimal lineBase = taxType == SpecificTaxType.FixedPerUnit ? line.Quantity : line.AmountHT;

            decimal lineShare;
            if (i == lines.Count - 1)
                lineShare = totalAmount - distributed;
            else
            {
                lineShare = Math.Round(totalAmount * (lineBase / distributionBase), 2);
                distributed += lineShare;
            }

            line.TaxSpecificAmount = lineShare;
            line.AmountTTC += lineShare;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  🆕 VALIDATION — Async (credit note lookup)
    // ══════════════════════════════════════════════════════════

    private async Task<ValidationResult> ValidateInvoiceAsync(Invoice invoice)
    {
        // ── Basic rules ──
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

        // ── OnTotal coherence ──
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

        // ── Client rules ──
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

        // ── Group D → Comment A ──
        if (invoice.Lines.Any(l => l.TaxGroup == TaxGroup.D)
            && string.IsNullOrWhiteSpace(invoice.CommentA))
            return new("La référence du document de dérogation DGI (Ligne A) est obligatoire pour le groupe D.");

        // ══════════════════════════════════════════════════════════
        //  🆕 EXPORT — Les types EV/EA/ET doivent utiliser le groupe E
        // ══════════════════════════════════════════════════════════
        if (invoice.Type.IsExport())
        {
            var nonExportLines = invoice.Lines
                .Where(l => l.TaxGroup != TaxGroup.E && l.TaxGroup != TaxGroup.L && l.TaxGroup != TaxGroup.N)
                .ToList();
            if (nonExportLines.Any())
                return new($"Facture d'exportation : les articles doivent être dans le groupe E (Exportation). " +
                           $"Article « {nonExportLines.First().Name} » est dans le groupe {nonExportLines.First().TaxGroup}.");
        }

        // ══════════════════════════════════════════════════════════
        //  🆕 FACTURE D'AVOIR — Validation complète (§25a-25e)
        // ══════════════════════════════════════════════════════════
        if (invoice.Type == InvoiceType.FA || invoice.Type == InvoiceType.EA)
        {
            if (invoice.CreditNoteNature == null)
                return new("La nature de la facture d'avoir est obligatoire.");

            if (string.IsNullOrWhiteSpace(invoice.OriginalInvoiceReference))
                return new("La référence de la facture originale est obligatoire.");

            // §27 — RRR : la référence doit être exactement "RRR"
            if (invoice.CreditNoteNature == Domain.Enums.CreditNoteNature.RRR)
            {
                if (invoice.OriginalInvoiceReference.Trim().ToUpper() != "RRR")
                    return new("Pour une facture d'avoir de type RRR (Rabais/Remise/Ristourne), " +
                               "la référence de la facture originale doit être « RRR ».");
                // RRR = pas de validation article-par-article
            }
            else
            {
                // §25a — La référence doit exister et être valide
                var originalInvoice = await _unitOfWork.Invoices.GetByCodeDEFDGIAsync(
                    invoice.OriginalInvoiceReference.Trim());

                if (originalInvoice == null)
                    return new($"Facture originale introuvable pour le Code DEF/DGI « {invoice.OriginalInvoiceReference} ».");

                if (originalInvoice.Status != InvoiceStatus.Normalized)
                    return new($"La facture originale « {invoice.OriginalInvoiceReference} » n'est pas normalisée.");

                if (originalInvoice.Type.IsCreditNote())
                    return new("Impossible de créer une facture d'avoir sur une autre facture d'avoir.");

                // Cohérence export : FA→FV, EA→EV
                if (invoice.Type == InvoiceType.EA && !originalInvoice.Type.IsExport())
                    return new("Une facture d'avoir à l'exportation (EA) doit référencer une facture d'exportation.");
                if (invoice.Type == InvoiceType.FA && originalInvoice.Type.IsExport())
                    return new("Utilisez le type EA pour les avoirs sur factures d'exportation.");

                // §25d — Quantités cumulées déjà remboursées
                var cumulativeRefunded = await GetCumulativeRefundedQuantitiesAsync(
                    invoice.OriginalInvoiceReference.Trim());

                foreach (var line in invoice.Lines)
                {
                    // §25b — L'article doit exister sur la facture originale
                    var originalLine = originalInvoice.Lines.FirstOrDefault(
                        ol => ol.Code.Equals(line.Code, StringComparison.OrdinalIgnoreCase));

                    if (originalLine == null)
                        return new($"L'article « {line.Name} » (code: {line.Code}) n'existe pas " +
                                   $"sur la facture originale {invoice.OriginalInvoiceReference}.");

                    // §25c — Quantité ≤ quantité originale
                    if (line.Quantity > originalLine.Quantity)
                        return new($"L'article « {line.Name} » : quantité à rembourser ({line.Quantity:G}) " +
                                   $"dépasse la quantité originale ({originalLine.Quantity:G}).");

                    // §25d — Quantités cumulées ≤ quantité originale
                    decimal alreadyRefunded = cumulativeRefunded.GetValueOrDefault(line.Code, 0m);
                    if (alreadyRefunded + line.Quantity > originalLine.Quantity)
                        return new($"L'article « {line.Name} » : cumul des remboursements " +
                                   $"({alreadyRefunded:G} + {line.Quantity:G} = {alreadyRefunded + line.Quantity:G}) " +
                                   $"dépasse la quantité originale ({originalLine.Quantity:G}).");

                    // §25e — Prix unitaire identique à l'original
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

                // Store the original invoice ID for traceability
                invoice.OriginalInvoiceId = originalInvoice.Id;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  🆕 FACTURE D'ACOMPTE — Validation
        // ══════════════════════════════════════════════════════════
        if (invoice.Type == InvoiceType.FT || invoice.Type == InvoiceType.ET)
        {
            // Advance invoices must have at least one payment
            if (invoice.Payments.Count == 0)
                return new("Une facture d'acompte doit contenir au moins un paiement.");

            // If linked to a group, validate cumulative doesn't exceed target
            // (This is optional business logic — the spec doesn't mandate it)
        }

        // ── Payment total ──
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

            // ── Devise : toujours renseignée ──
            CurrencyCode = string.IsNullOrEmpty(invoice.CurrencyCode)
                ? "CDF" : invoice.CurrencyCode,
            CurrencyRate = invoice.CurrencyRate,
            CurrencyDate = invoice.CurrencyDate ?? DateTime.Now,

            // ── Commentaires : toujours renseignés ("" si vide) ──
            CommentA = invoice.CommentA ?? "",
            CommentB = invoice.CommentB ?? "",
            CommentC = invoice.CommentC ?? "",
            CommentD = invoice.CommentD ?? "",
            CommentE = invoice.CommentE ?? "",
            CommentF = invoice.CommentF ?? "",
            CommentG = invoice.CommentG ?? "",
            CommentH = invoice.CommentH ?? ""
        };

        // ── Référence FA/EA : toujours renseignée ("" si non-avoir) ──
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

        // ── Client : toujours présent ──
        request.Client = new FiscalClientInfo
        {
            Type = invoice.ClientType.ToString(),
            TypeDesc = GetClientTypeDesc(invoice.ClientType),
            NIF = invoice.ClientNIF ?? "",
            Name = string.IsNullOrWhiteSpace(invoice.ClientName)
                ? invoice.ClientType.ToString()    // "PP" si anonyme (conforme au sample)
                : invoice.ClientName,
            Address = invoice.ClientAddress ?? "",
            Contact = string.Join(" ", new[] { invoice.ClientPhone, invoice.ClientEmail }
                .Where(s => !string.IsNullOrEmpty(s)))
        };

        // ── Articles ──
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            decimal baseUnitPrice = invoice.PriceMode == PriceMode.TTC
                ? line.UnitPriceTTC : line.UnitPriceHT;

            // ⚠ rawPrice = toujours le prix unitaire brut (pleine précision)
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
                TaxSpecificValue = fiscalTsValue,                          // null → "0%" dans MapToDto
                TaxSpecificAmount = line.TaxSpecificAmount,                // 0 si aucune
                OriginalPrice = rawPrice,                                   // ⚠ TOUJOURS (pleine précision)
                PriceModification = priceModification                       // ⚠ TOUJOURS ("" si pas de modif)
            });
        }

        // ── Paiements ──
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
                CurrencyCode = paymentCurrency,         // ⚠ Toujours présent
                CurrencyRate = paymentRate               // ⚠ Toujours présent
            });
        }

        return request;
    }

    // ═══════════════════════════════════════════
    // 🆕 Credit Note Nature → Description (DGI 2026 §III)
    // ═══════════════════════════════════════════
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