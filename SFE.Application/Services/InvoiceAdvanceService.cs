using SFE.Application.Interfaces;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.Application.Services;

public class InvoiceAdvanceService : IInvoiceAdvanceService
{
    private readonly ITimeProvider _time;

    public InvoiceAdvanceService(ITimeProvider time)
    {
        _time = time;
    }

    public Invoice BuildAdvanceInvoice(AdvanceBuildContext ctx)
    {
        // ── Guards ──
        if (string.IsNullOrWhiteSpace(ctx.AdvanceGroupId))
            throw new AdvanceChainException("AdvanceGroupId est requis.");
        if (ctx.AdvanceAmount <= 0)
            throw new AdvanceChainException("Le montant de l'acompte doit être > 0.");
        if (ctx.OrderTotal <= 0)
            throw new AdvanceChainException("Le total de la commande doit être > 0.");
        if (ctx.PreviousAdvancesTotal < 0)
            throw new AdvanceChainException("Les acomptes antérieurs ne peuvent être négatifs.");

        var alreadyPlusNew = ctx.PreviousAdvancesTotal + ctx.AdvanceAmount;
        if (alreadyPlusNew > ctx.OrderTotal + 0.01m)
            throw new AdvanceChainException(
                $"Acomptes ({alreadyPlusNew:N2}) > total commande ({ctx.OrderTotal:N2}).");

        // ── Sum of payments must equal AdvanceAmount ──
        var paid = ctx.Payments?.Sum(p => p.Amount) ?? 0;
        if (Math.Abs(paid - ctx.AdvanceAmount) > 0.01m)
            throw new AdvanceChainException(
                $"Paiements ({paid:N2}) ≠ acompte ({ctx.AdvanceAmount:N2}).");

        // ── Compute fiscal split for the synthetic line ──
        // Per DGI: Acompte amount is TTC. We back-compute HT, TVA.
        var rate = ctx.DominantTaxRate;
        var ttc = ctx.AdvanceAmount;
        var ht = Math.Round(ttc / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero);
        var tva = Math.Round(ttc - ht, 2, MidpointRounding.AwayFromZero);

        var line = new InvoiceLine
        {
            LineNumber = 1,
            Code = "ACOMPTE",
            Name = "Acompte sur commande",
            ItemType = ItemType.SER,
            TaxGroup = ctx.DominantTaxGroup,
            TaxRate = rate,
            UnitPriceHT = ht,
            UnitPriceTTC = ttc,
            UnitPrice = ttc,
            Quantity = 1m,
            Unit = "lot",
            DiscountType = DiscountType.None,
            SpecificTaxType = SpecificTaxType.None,
            AmountHTBeforeDiscount = ht,
            AmountHT = ht,
            AmountTVA = tva,
            AmountTTC = ttc,
        };

        var invoice = new Invoice
        {
            Type = ctx.IsExport ? InvoiceType.ET : InvoiceType.FT,
            Status = InvoiceStatus.Draft,
            PriceMode = ctx.PriceMode,

            // ⚠ DGI §1.1 — anti-fraud timestamp via ITimeProvider.
            // Invoice.CreatedAt is DateTimeOffset: the offset is preserved so
            // Lubumbashi (+02:00) and Kinshasa (+01:00) entries remain
            // unambiguous even when cross-queried.
            CreatedAt = _time.UtcNow,

            AdvanceGroupId = ctx.AdvanceGroupId,
            OrderTotal = ctx.OrderTotal,
            PreviousAdvancesTotal = ctx.PreviousAdvancesTotal,
            AdvanceAmount = ctx.AdvanceAmount,
            RemainingAfterAdvance = ctx.OrderTotal - alreadyPlusNew,

            ClientType = ctx.ClientType,
            ClientName = ctx.ClientName,
            ClientNIF = ctx.ClientNIF,

            OperatorId = ctx.OperatorId,
            OperatorName = ctx.OperatorName,
            PointOfSaleId = ctx.PointOfSaleId,

            // Order reference goes in CommentA per DGI guidance
            CommentA = string.IsNullOrWhiteSpace(ctx.OrderReference)
                       ? string.Empty
                       : $"Réf. commande : {ctx.OrderReference}",
            CommentB = $"Total commande : {ctx.OrderTotal:N2} CDF",
            CommentC = ctx.PreviousAdvancesTotal > 0
                       ? $"Acomptes antérieurs : {ctx.PreviousAdvancesTotal:N2} CDF"
                       : string.Empty,
            CommentD = $"Solde après cet acompte : {ctx.OrderTotal - alreadyPlusNew:N2} CDF",

            // Totals == single line
            TotalHTBeforeDiscount = ht,
            TotalHT = ht,
            TotalTVA = tva,
            TotalTTC = ttc,
        };

        invoice.Lines.Add(line);
        line.Invoice = invoice;

        if (ctx.Payments != null)
        {
            foreach (var p in ctx.Payments)
            {
                p.Invoice = invoice;
                invoice.Payments.Add(p);
            }
        }

        return invoice;
    }

    public void ValidateChain(
        Invoice finalInvoice,
        IReadOnlyList<Invoice> previousAdvances)
    {
        if (finalInvoice == null)
            throw new ArgumentNullException(nameof(finalInvoice));

        if (finalInvoice.Type is not (InvoiceType.FV or InvoiceType.EV))
            throw new AdvanceChainException(
                "La facture finale doit être de type FV ou EV.");

        if (string.IsNullOrWhiteSpace(finalInvoice.AdvanceGroupId))
            throw new AdvanceChainException(
                "La facture finale doit porter un AdvanceGroupId.");

        // ── All advances must share the same group ──
        foreach (var a in previousAdvances)
        {
            if (a.AdvanceGroupId != finalInvoice.AdvanceGroupId)
                throw new AdvanceChainException(
                    $"L'acompte {a.InvoiceNumber} a un groupe différent.");
            if (a.Status != InvoiceStatus.Normalized)
                throw new AdvanceChainException(
                    $"L'acompte {a.InvoiceNumber} n'est pas normalisé.");
            if (a.Type is not (InvoiceType.FT or InvoiceType.ET))
                throw new AdvanceChainException(
                    $"{a.InvoiceNumber} n'est pas une facture d'acompte.");
        }

        // ── Export consistency ──
        bool finalExport = finalInvoice.Type == InvoiceType.EV;
        if (previousAdvances.Any(a => (a.Type == InvoiceType.ET) != finalExport))
            throw new AdvanceChainException(
                "Mélange interdit entre acomptes locaux (FT) et export (ET).");

        // ── Sum check ──
        var totalAdvances = previousAdvances.Sum(a => a.AdvanceAmount);
        if (totalAdvances > finalInvoice.TotalTTC + 0.01m)
            throw new AdvanceChainException(
                $"Acomptes cumulés ({totalAdvances:N2}) > total final ({finalInvoice.TotalTTC:N2}).");

        // ── Final payments sanity (advance-virtual + real) ──
        var paid = finalInvoice.Payments?.Sum(p => p.Amount) ?? 0;
        if (Math.Abs(paid - finalInvoice.TotalTTC) > 0.01m)
            throw new AdvanceChainException(
                $"Paiements ({paid:N2}) ≠ total facture ({finalInvoice.TotalTTC:N2}).");
    }

    public IReadOnlyList<InvoicePayment> BuildAdvancePayments(
        IReadOnlyList<Invoice> previousAdvances)
    {
        var list = new List<InvoicePayment>();
        foreach (var a in previousAdvances.OrderBy(x => x.CreatedAt))
        {
            // Use the dominant payment of each advance, or fallback Especes.
            var primary = a.Payments?.OrderByDescending(p => p.Amount).FirstOrDefault();
            list.Add(new InvoicePayment
            {
                PaymentType = primary?.PaymentType ?? PaymentType.Especes,
                Amount = a.AdvanceAmount,
                CurrencyCode = primary?.CurrencyCode ?? "CDF",
                CurrencyRate = primary?.CurrencyRate ?? 1m,
            });
        }
        return list;
    }
}