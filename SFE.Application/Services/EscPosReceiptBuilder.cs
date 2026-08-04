using System.Text;
using SFE.Domain.Abstractions;
using SFE.Domain.Entities;
using SFE.Domain.Enums;
using SFE.Application;
using SFE.Application.Helpers;

namespace SFE.Application.Services;

/// <summary>
/// Builds an ESC/POS byte stream for thermal printers (58mm or 80mm).
/// Includes ALL DGI 2026 1.2 mandatory mentions.
///
/// DGI 1.1 - every timestamp MUST be routed through <see cref="ITimeProvider"/>.
/// The invoice stores UTC; this builder converts to app-local (Kinshasa UTC+1 by default).
/// </summary>
public static class EscPosReceiptBuilder
{
    // === ESC/POS constants ===
    private static readonly byte[] INIT = { 0x1B, 0x40 };
    private static readonly byte[] ALIGN_LEFT = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] ALIGN_CENTER = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] ALIGN_RIGHT = { 0x1B, 0x61, 0x02 };
    private static readonly byte[] BOLD_ON = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BOLD_OFF = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] DOUBLE_ON = { 0x1D, 0x21, 0x11 };
    private static readonly byte[] DOUBLE_H_ON = { 0x1D, 0x21, 0x01 };
    private static readonly byte[] SIZE_NORMAL = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] UNDERLINE_ON = { 0x1B, 0x2D, 0x01 };
    private static readonly byte[] UNDERLINE_OFF = { 0x1B, 0x2D, 0x00 };
    private static readonly byte[] FEED_3 = { 0x1B, 0x64, 0x03 };
    private static readonly byte[] FEED_5 = { 0x1B, 0x64, 0x05 };
    private static readonly byte[] CUT_PARTIAL = { 0x1D, 0x56, 0x42, 0x03 };
    private static readonly byte[] LF = { 0x0A };

    private static readonly System.Globalization.CultureInfo MONEY =
        System.Globalization.CultureInfo.InvariantCulture;

    private static string Fmt(decimal v) => v.ToString("N2", MONEY);
    private static string FmtCompact(decimal v) => v.ToString("N2", MONEY);
    private static string Qty(decimal v) => v.ToString("N3", MONEY);
    private static string Rate(decimal v) => v.ToString("0.00", MONEY);

    private static decimal Signed(decimal v, bool negate) => negate ? -v : v;

    private class ReceiptContext
    {
        public int Width { get; set; } = 48;
        public int CodePage { get; set; } = 858;
        public string FooterText { get; set; } = "Merci pour votre achat !";
        public bool PrintLogo { get; set; }
        public DateTimeOffset PrintedAt { get; set; }
        public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;
    }

    // =======================================================
    //  PUBLIC: Build full receipt
    // =======================================================

    /// <summary>
    /// Builds the full ESC/POS byte stream for a receipt.
    /// </summary>
    /// <param name="asProforma">
    /// If <c>true</c>, forces proforma rendering regardless of <see cref="Invoice.Type"/>.
    /// Skips the fiscal (CodeDEFDGI/NIM/QR) block and prints the "document non fiscal"
    /// banner. Safe to call before normalization — no fiscal fields required.
    /// </param>
    public static byte[] Build(
        Invoice invoice,
        Company company,
        PointOfSale? pos,
        ITimeProvider time,
        decimal exchangeRate = 0m,
        bool isDuplicate = false,
        bool asProforma = false,
        int? overridePaperWidthMm = null)
    {
        if (time is null) throw new ArgumentNullException(nameof(time));

        // Proforma if explicitly requested OR the invoice type says so.
        bool renderAsProforma = asProforma || invoice.Type == InvoiceType.PRO;

        int paperWidth = overridePaperWidthMm ?? pos?.PaperWidthMm ?? 80;
        int charsPerLine = paperWidth >= 80 ? 48 : 32;
        int codePage = pos?.PrinterCodePage ?? 858;
        string footer = pos?.ReceiptFooterText ?? "Merci pour votre achat !";

        var ctx = new ReceiptContext
        {
            Width = charsPerLine,
            CodePage = codePage,
            FooterText = footer,
            PrintLogo = pos?.PrintLogo ?? false,
            PrintedAt = time.LocalNow,
            Zone = time.AppTimeZone
        };

        var ms = new MemoryStream();
        Write(ms, INIT);

        byte cpByte = codePage switch
        {
            437 => 0x00,
            850 => 0x02,
            858 => 0x13,
            1252 => 0x10,
            _ => 0x13
        };
        ms.Write(new byte[] { 0x1B, 0x74, cpByte });

        // ======= COMPANY LOGO =======
        if (ctx.PrintLogo
            && company.Logo != null
            && company.Logo.Length > 0
            && OperatingSystem.IsWindows())
        {
            Write(ms, ALIGN_CENTER);
            WriteBitmapLogo(ms, company.Logo);
            Write(ms, LF);
        }

        WriteCompanyHeader(ms, company, pos, ctx);
        WriteInvoiceTypeBanner(ms, invoice, isDuplicate, renderAsProforma, ctx);
        WriteInvoiceMeta(ms, invoice, company, ctx);
        WriteClientSection(ms, invoice, ctx);
        WriteDashLine(ms, ctx);
        WriteLineItems(ms, invoice, ctx);
        WriteTaxBreakdown(ms, invoice, ctx);
        WriteAdvanceBlock(ms, invoice, ctx);

        if (invoice.TotalSpecificTax > 0)
        {
            WriteDoubleLine(ms, ctx);
            WriteRow(ms, "TAXE SPECIFIQUE", Fmt(invoice.TotalSpecificTax), ctx);
        }
        bool neg = invoice.IsCreditNote;
        // ======= (p) TOTAL TTC =======
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_H_ON);
        WriteRow(ms, "TOTAL TTC", $"{Fmt(Signed(invoice.TotalTTC, neg))} CDF", ctx);
        Write(ms, SIZE_NORMAL);
        Write(ms, BOLD_OFF);

        // ======= (q) AMOUNT IN WORDS =======
        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);
        WriteText(ms, "Arrete a la somme de:", ctx);
        Write(ms, BOLD_ON);
        WriteText(ms, NumberToFrenchWords.Convert(invoice.TotalTTC), ctx);
        Write(ms, BOLD_OFF);

        // ======= (r) USD + EXCHANGE RATE =======
        if (exchangeRate > 0)
        {
            WriteDashLine(ms, ctx);
            decimal usd = Math.Round(invoice.TotalTTC / exchangeRate, 2);
            WriteRow(ms, "Taux de change", $"1 USD = {Fmt(exchangeRate)} CDF", ctx);
            WriteRow(ms, "Montant USD", $"{Fmt(Signed(usd,neg))} USD", ctx);
        }
        else if (!string.IsNullOrEmpty(invoice.CurrencyCode) &&
                 invoice.CurrencyCode != "CDF" && invoice.CurrencyRate > 0)
        {
            WriteDashLine(ms, ctx);
            decimal alt = Math.Round(invoice.TotalTTC / invoice.CurrencyRate, 2);
            WriteRow(ms, "Taux de change",
                $"1 {invoice.CurrencyCode} = {Fmt(invoice.CurrencyRate)} CDF", ctx);
            WriteRow(ms, $"Montant {invoice.CurrencyCode}", Fmt(alt), ctx);
        }

        // ======= (s) PAYMENT MODES =======
        // For a preview proforma, invoice.Payments may be empty — that's fine,
        // we just skip the section header entirely.
        if (invoice.Payments != null && invoice.Payments.Count > 0)
        {
            WriteDashLine(ms, ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, "MODE(S) DE PAIEMENT", ctx);
            Write(ms, BOLD_OFF);
            foreach (var pay in invoice.Payments)
                WriteRow(ms, GetPaymentLabel(pay.PaymentType), Fmt(pay.Amount), ctx);
        }

        // ======= (u)(v)(w) DATE, ISF, OPERATOR =======
        WriteDoubleLine(ms, ctx);
        WriteRow(ms, "Date", FormatUtcAsLocal(invoice.CreatedAt, ctx), ctx);
        WriteRow(ms, "ISF", invoice.ISF, ctx);
        WriteRow(ms, "Operateur", invoice.OperatorName, ctx);

        // ======= SECURITY - never for proforma =======
        if (!renderAsProforma && !string.IsNullOrEmpty(invoice.CodeDEFDGI))
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, ALIGN_CENTER);
            Write(ms, BOLD_ON);
            WriteText(ms, "-- FACTURE NORMALISEE --", ctx);
            Write(ms, BOLD_OFF);
            Write(ms, LF);

            Write(ms, ALIGN_LEFT);
            WriteRow(ms, "Code DEF/DGI:", "", ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, invoice.CodeDEFDGI, ctx);
            Write(ms, BOLD_OFF);

            if (!string.IsNullOrEmpty(invoice.NIM))
                WriteRow(ms, "NIM", invoice.NIM, ctx);
            if (!string.IsNullOrEmpty(invoice.Counters))
                WriteRow(ms, "Compteurs", invoice.Counters, ctx);
            if (invoice.NormalizedAt.HasValue)
                WriteRow(ms, "Normalisee",
                    FormatUtcAsLocal(invoice.NormalizedAt.Value, ctx), ctx);

            if (!string.IsNullOrEmpty(invoice.QRCodeContent))
            {
                Write(ms, LF);
                Write(ms, ALIGN_CENTER);
                WriteQrCode(ms, invoice.QRCodeContent);
                Write(ms, LF);
            }
        }
        else if (renderAsProforma)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, ALIGN_CENTER);
            Write(ms, BOLD_ON);
            WriteText(ms, "Cette proforma ne tient pas lieu", ctx);
            WriteText(ms, "de facture fiscale.", ctx);
            Write(ms, BOLD_OFF);
            if (invoice.ProformaValidUntil.HasValue)
                WriteText(ms,
                    $"Valable jusqu'au {invoice.ProformaValidUntil:dd/MM/yyyy}", ctx);
        }

        WriteComments(ms, invoice, ctx);

        // ======= FOOTER =======
        Write(ms, LF);
        Write(ms, ALIGN_CENTER);
        WriteText(ms, ctx.FooterText, ctx);
        WriteText(ms, $"Imprime le {ctx.PrintedAt:dd/MM/yyyy HH:mm}", ctx);
        WriteText(ms, "Conforme DGI-RDC 2026", ctx);
        WriteDashLine(ms, ctx);

        Write(ms, FEED_5);
        Write(ms, CUT_PARTIAL);

        return ms.ToArray();
    }

    // =======================================================
    //  DATE FORMATTING
    // =======================================================

    private static string FormatUtcAsLocal(DateTimeOffset utc, ReceiptContext ctx)
        => TimeZoneInfo.ConvertTime(utc, ctx.Zone).ToString("dd/MM/yyyy HH:mm:ss");

    private static string FormatUtcAsLocal(DateTime utc, ReceiptContext ctx)
    {
        var asUtc = utc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, ctx.Zone)
                           .ToString("dd/MM/yyyy HH:mm:ss");
    }

    // =======================================================
    //  SECTIONS
    // =======================================================

    private static void WriteCompanyHeader(
        MemoryStream ms, Company company, PointOfSale? pos, ReceiptContext ctx)
    {
        Write(ms, ALIGN_CENTER);

        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_ON);
        WriteText(ms, company.Name, ctx);
        Write(ms, SIZE_NORMAL);
        Write(ms, BOLD_OFF);

        Write(ms, BOLD_ON);
        WriteText(ms, $"NIF: {company.NIF}", ctx);
        Write(ms, BOLD_OFF);

        if (!string.IsNullOrWhiteSpace(company.RCCM))
            WriteText(ms, $"RCCM: {company.RCCM}", ctx);

        if (pos != null)
        {
            Write(ms, BOLD_ON);
            WriteText(ms, "POINT DE VENTE", ctx);
            Write(ms, BOLD_OFF);

            if (!string.IsNullOrWhiteSpace(pos.Name))
                WriteText(ms, pos.Name, ctx);
            if (!string.IsNullOrWhiteSpace(pos.Address))
                WriteText(ms, pos.Address, ctx);
            if (!string.IsNullOrWhiteSpace(pos.City))
                WriteText(ms, pos.City, ctx);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(company.Address))
                WriteText(ms, company.Address, ctx);
            if (!string.IsNullOrWhiteSpace(company.City))
                WriteText(ms, company.City, ctx);
        }

        if (!string.IsNullOrWhiteSpace(company.Email))
            WriteText(ms, company.Email, ctx);
        if (!string.IsNullOrWhiteSpace(company.Phone))
            WriteText(ms, company.Phone, ctx);

        WriteText(ms, $"ISF: {company.ISF}", ctx);
    }

    private static void WriteInvoiceTypeBanner(
        MemoryStream ms, Invoice invoice, bool isDuplicate,
        bool renderAsProforma, ReceiptContext ctx)
    {
        Write(ms, ALIGN_CENTER);
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        Write(ms, DOUBLE_H_ON);

        if (renderAsProforma)
        {
            WriteText(ms, "FACTURE PROFORMA", ctx);
            Write(ms, SIZE_NORMAL);
            WriteText(ms, "(Document non fiscal)", ctx);
            Write(ms, BOLD_OFF);
            WriteDoubleLine(ms, ctx);
            return;
        }

        string title = invoice.Type switch
        {
            InvoiceType.FV => "FACTURE DE VENTE",
            InvoiceType.FT => "FACTURE D'ACOMPTE OU D'AVANCE",
            InvoiceType.EV => "FACTURE DE VENTE",
            InvoiceType.ET => "FACTURE D'ACOMPTE OU D'AVANCE",
            InvoiceType.FA => "FACTURE D'AVOIR",
            InvoiceType.EA => "FACTURE D'AVOIR",
            _ => "FACTURE"
        };

        WriteText(ms, title, ctx);
        Write(ms, SIZE_NORMAL);

        if (invoice.Type is InvoiceType.EV or InvoiceType.ET or InvoiceType.EA)
        {
            Write(ms, BOLD_ON);
            WriteText(ms, "A L'EXPORTATION", ctx);
            Write(ms, BOLD_OFF);
        }

        if (invoice.Type is InvoiceType.FA or InvoiceType.EA
            && invoice.CreditNoteNature.HasValue)
        {
            string nature = invoice.CreditNoteNature.Value switch
            {
                CreditNoteNature.COR => "COR (Correction)",
                CreditNoteNature.RAN => "RAN (Annulation)",
                CreditNoteNature.RAM => "RAM (Avoir suite reprise de biens/services)",
                CreditNoteNature.RRR => "RRR (Remise, Ristourne, Rabais)",
                _ => ""
            };
            WriteText(ms, $"Nature: {nature}", ctx);

            if (!string.IsNullOrEmpty(invoice.OriginalInvoiceReference))
                WriteText(ms, $"Ref. orig.: {invoice.OriginalInvoiceReference}", ctx);
        }

        if (isDuplicate)
        {
            Write(ms, BOLD_ON);
            Write(ms, DOUBLE_H_ON);
            WriteText(ms, "*** DUPLICATA ***", ctx);
            Write(ms, SIZE_NORMAL);
            Write(ms, BOLD_OFF);
        }

        Write(ms, BOLD_OFF);
        WriteDoubleLine(ms, ctx);
    }

    private static void WriteInvoiceMeta(
        MemoryStream ms, Invoice invoice, Company company, ReceiptContext ctx)
    {
        Write(ms, ALIGN_LEFT);

        string regime = company.DefaultPriceMode == PriceMode.TTC
            ? "MODE PRIX TTC" : "MODE PRIX HT";
        WriteRow(ms, "Regime", regime, ctx);

        Write(ms, BOLD_ON);
        WriteRow(ms, "Num Facture", invoice.InvoiceNumber, ctx);
        Write(ms, BOLD_OFF);

        WriteRow(ms, "Type", $"{invoice.Type} - {GetTypeLabel(invoice.Type)}", ctx);
        WriteRow(ms, "Date", FormatUtcAsLocal(invoice.CreatedAt, ctx), ctx);
    }

    private static void WriteClientSection(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);

        string typeMention = invoice.ClientType switch
        {
            ClientType.PP => "[PP] Personne physique",
            ClientType.PM => "[PM] Personne Morale",
            ClientType.PC => "[PC] Pers. phys. commercante",
            ClientType.PL => "[PL] Profession liberale",
            ClientType.AO => "[AO] Ambassades / Org. int.",
            _ => ""
        };

        WriteRow(ms, "Client", typeMention, ctx);

        if (!string.IsNullOrWhiteSpace(invoice.ClientName))
            WriteRow(ms, "Nom", invoice.ClientName, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientNIF))
            WriteRow(ms, "NIF", invoice.ClientNIF, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientPhone))
            WriteRow(ms, "Tel", invoice.ClientPhone, ctx);
        if (!string.IsNullOrWhiteSpace(invoice.ClientAddress))
            WriteRow(ms, "Adresse", invoice.ClientAddress, ctx);
    }

    private static void WriteLineItems(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        Write(ms, ALIGN_LEFT);
        Write(ms, BOLD_ON);

        string totalLabel = invoice.PriceMode == PriceMode.TTC ? "TTC" : "HT";

        int totalColWidth = ctx.Width >= 48 ? 12 : 10;
        int totalEnd = ctx.Width;
        int qtyEnd = totalEnd - totalColWidth;
        bool neg = invoice.IsCreditNote;

        var hsb = new StringBuilder("#Nom");
        int padA = qtyEnd - "Qte x P.U.".Length - hsb.Length;
        if (padA < 1) padA = 1;
        hsb.Append(' ', padA).Append("Qte x P.U.");
        int padB = totalEnd - totalLabel.Length - hsb.Length;
        if (padB < 1) padB = 1;
        hsb.Append(' ', padB).Append(totalLabel);

        WriteText(ms, hsb.ToString(), ctx);
        Write(ms, BOLD_OFF);
        WriteDashLine(ms, ctx);

        foreach (var ln in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            string grpLabel = GroupLabel(ln.TaxGroup, ln.TaxGroupAType);
            string namePart = $"{ln.LineNumber} {ln.Name} ({grpLabel})";

            decimal unitPrice = GetEffectiveUnitPrice(ln, invoice.PriceMode);
            decimal totalAmount = invoice.PriceMode == PriceMode.TTC
                                    ? ln.GrossAmountTTC : ln.GrossAmountHT;

            string qtyPart = $"{Qty(ln.Quantity)} x {FmtCompact(unitPrice)}";
            string totalPart = FmtCompact(Signed(totalAmount, neg));

            int maxNameForSingleLine = qtyEnd - qtyPart.Length - 1;

            if (namePart.Length <= maxNameForSingleLine)
            {
                WriteThreeCol(ms, namePart, qtyPart, totalPart,
                              qtyEnd, totalEnd, ctx);
            }
            else
            {
                WriteText(ms, namePart, ctx);
                WriteThreeCol(ms, "", qtyPart, totalPart,
                              qtyEnd, totalEnd, ctx);
            }

            if (ln.DiscountType != DiscountType.None && ln.DiscountAmount > 0)
            {
                string discDesc = ln.DiscountType == DiscountType.Percentage
                    ? $"   Remise {Rate(ln.DiscountValue)}%"
                    : "   Remise";
                WriteAlignedRow(ms, discDesc, $"-{Fmt(ln.DiscountAmount)}",
                                totalEnd, ctx);
            }

            if (ln.TaxSpecificAmount > 0)
            {
                string tsDesc = ln.SpecificTaxType == SpecificTaxType.Percentage
                    ? $"   T.S. ({Rate(ln.SpecificTaxValue)}%)"
                    : "   T.S.";
                WriteAlignedRow(ms, tsDesc, Fmt(Signed(ln.TaxSpecificAmount, neg)),
                                totalEnd, ctx);
            }
        }
    }

    private static void WriteThreeCol(
        MemoryStream ms,
        string left, string middle, string right,
        int middleEndCol, int rightEndCol,
        ReceiptContext ctx)
    {
        left ??= string.Empty;
        middle ??= string.Empty;
        right ??= string.Empty;

        int middleStart = middleEndCol - middle.Length;
        int minMiddleStart = left.Length == 0 ? 0 : left.Length + 1;
        if (middleStart < minMiddleStart) middleStart = minMiddleStart;

        int rightStart = rightEndCol - right.Length;
        int minRightStart = middleStart + middle.Length + 1;
        if (rightStart < minRightStart) rightStart = minRightStart;

        var sb = new StringBuilder();
        sb.Append(left);
        if (sb.Length < middleStart) sb.Append(' ', middleStart - sb.Length);
        sb.Append(middle);
        if (sb.Length < rightStart) sb.Append(' ', rightStart - sb.Length);
        sb.Append(right);

        string line = sb.ToString();
        if (line.Length > ctx.Width) line = line[..ctx.Width];
        WriteText(ms, line, ctx);
    }

    private static void WriteAlignedRow(
        MemoryStream ms, string label, string value, int valueEndCol, ReceiptContext ctx)
    {
        label ??= string.Empty;
        value ??= string.Empty;

        int valueStart = valueEndCol - value.Length;
        if (valueStart < 1) valueStart = 1;

        if (label.Length > valueStart - 1)
            label = label[..(valueStart - 1)];

        int pad = valueStart - label.Length;
        if (pad < 1) pad = 1;

        string line = label + new string(' ', pad) + value;
        if (line.Length > ctx.Width) line = line[..ctx.Width];

        WriteText(ms, line, ctx);
    }

    private static void WriteTaxBreakdown(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        bool neg = invoice.IsCreditNote;
        WriteDoubleLine(ms, ctx);
        Write(ms, BOLD_ON);
        WriteText(ms, " DETAIL FISCAL PAR GROUPE", ctx);
        Write(ms, BOLD_OFF);
        WriteDashLine(ms, ctx);

        var groups = invoice.Lines
            .GroupBy(l => l.TaxGroup)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            char letter = (char)('A' + (int)g.Key);
            decimal rate = g.First().TaxRate;
            decimal groupHT = g.Sum(l => l.AmountHT);
            decimal groupTVA = g.Sum(l => l.AmountTVA);

            string rateCode = $"{Rate(rate)}%";

            WriteRow(ms, $"TOTAL H.T. [{letter}] Taxable {rateCode}", Fmt(groupHT), ctx);
            WriteRow(ms, $"TOTAL TVA [{letter}] Taxable {rateCode}", Fmt(groupTVA), ctx);
        }

        WriteDashLine(ms, ctx);
        WriteRow(ms, "Total HT", Fmt(Signed(invoice.TotalHT, neg)), ctx);
        WriteRow(ms, "Total TVA", Fmt(Signed(invoice.TotalTVA, neg)), ctx);
    }

    private static void WriteAdvanceBlock(MemoryStream ms, Invoice inv, ReceiptContext ctx)
    {
        if (inv.IsAdvanceInvoice)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, " DETAIL ACOMPTE", ctx);
            Write(ms, BOLD_OFF);
            WriteRow(ms, "Total commande", Fmt(inv.OrderTotal), ctx);
            WriteRow(ms, "Acomptes anterieurs", Fmt(inv.PreviousAdvancesTotal), ctx);
            WriteRow(ms, "Acompte verse", Fmt(inv.AdvanceAmount), ctx);
            Write(ms, BOLD_ON);
            WriteRow(ms, "Reste a percevoir", Fmt(inv.RemainingAfterAdvance), ctx);
            Write(ms, BOLD_OFF);
            if (!string.IsNullOrWhiteSpace(inv.AdvanceGroupId))
                WriteText(ms, $" Ref projet: {inv.AdvanceGroupId}", ctx);
        }
        else if (inv.IsFinalWithAdvances)
        {
            WriteDoubleLine(ms, ctx);
            Write(ms, BOLD_ON);
            WriteText(ms, " SOLDE FINAL APRES ACOMPTES", ctx);
            Write(ms, BOLD_OFF);
            WriteRow(ms, "Total facture", Fmt(inv.TotalTTC), ctx);
            WriteRow(ms, "Acomptes percus", Fmt(inv.TotalAdvancesPaid), ctx);
            Write(ms, BOLD_ON);
            WriteRow(ms, "Solde du", Fmt(inv.RemainingBalance), ctx);
            Write(ms, BOLD_OFF);
        }
    }

    private static void WriteComments(
        MemoryStream ms, Invoice invoice, ReceiptContext ctx)
    {
        var comments = new (string id, string val)[]
        {
            ("A", invoice.CommentA), ("B", invoice.CommentB),
            ("C", invoice.CommentC), ("D", invoice.CommentD),
            ("E", invoice.CommentE), ("F", invoice.CommentF),
            ("G", invoice.CommentG), ("H", invoice.CommentH)
        };

        bool hasAny = comments.Any(c => !string.IsNullOrWhiteSpace(c.val));
        if (!hasAny) return;

        WriteDashLine(ms, ctx);
        Write(ms, ALIGN_LEFT);
        WriteText(ms, "COMMENTAIRES:", ctx);
        foreach (var (id, val) in comments)
        {
            if (!string.IsNullOrWhiteSpace(val))
                WriteText(ms, $" Ligne {id}: {val}", ctx);
        }
    }

    // =======================================================
    //  QR CODE
    // =======================================================
    private static void WriteQrCode(MemoryStream ms, string content, int paperWidthMm = 80)
    {
        byte[] data = Encoding.UTF8.GetBytes(content);
        byte module = paperWidthMm >= 80 ? (byte)0x06 : (byte)0x04; // smaller for 58 mm

        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, module });
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31 });

        int storeLen = data.Length + 3;
        byte sL = (byte)(storeLen % 256);
        byte sH = (byte)(storeLen / 256);
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, sL, sH, 0x31, 0x50, 0x30 });
        ms.Write(data);

        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
    }

    // =======================================================
    //  BITMAP LOGO
    // =======================================================
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void WriteBitmapLogo(MemoryStream ms, byte[] logoData)
    {
        try
        {
            using var logoMs = new MemoryStream(logoData);
            using var bitmap = new System.Drawing.Bitmap(logoMs);

            int maxDots = 384;
            int targetWidth = Math.Min(bitmap.Width, maxDots);
            targetWidth = (targetWidth / 8) * 8;
            if (targetWidth == 0) return;

            float scale = (float)targetWidth / bitmap.Width;
            int targetHeight = (int)(bitmap.Height * scale);

            using var scaled = new System.Drawing.Bitmap(targetWidth, targetHeight);
            using (var g = System.Drawing.Graphics.FromImage(scaled))
            {
                g.InterpolationMode =
                    System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
            }

            int widthBytes = targetWidth / 8;
            var rasterData = new byte[widthBytes * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    var pixel = scaled.GetPixel(x, y);
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;

                    if (brightness < 128)
                    {
                        int byteIndex = y * widthBytes + x / 8;
                        int bitIndex = 7 - (x % 8);
                        rasterData[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
            }

            byte xL = (byte)(widthBytes % 256);
            byte xH = (byte)(widthBytes / 256);
            byte yL = (byte)(targetHeight % 256);
            byte yH = (byte)(targetHeight / 256);

            ms.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH });
            ms.Write(rasterData);
        }
        catch { }
    }

    // =======================================================
    //  LOW-LEVEL HELPERS
    // =======================================================

    private static void Write(MemoryStream ms, byte[] data)
        => ms.Write(data, 0, data.Length);

    private static void WriteText(MemoryStream ms, string text, ReceiptContext ctx)
    {
        if (text.Length > ctx.Width)
        {
            foreach (var line in WordWrap(text, ctx.Width))
            {
                byte[] bytes = Encoding.GetEncoding(ctx.CodePage).GetBytes(line);
                ms.Write(bytes, 0, bytes.Length);
                ms.Write(LF, 0, LF.Length);
            }
        }
        else
        {
            byte[] bytes = Encoding.GetEncoding(ctx.CodePage).GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
            ms.Write(LF, 0, LF.Length);
        }
    }

    private static void WriteRow(
        MemoryStream ms, string label, string value, ReceiptContext ctx)
    {
        value ??= string.Empty;
        label ??= string.Empty;

        int maxLabel = ctx.Width - value.Length - 1;
        if (label.Length > maxLabel && maxLabel > 0)
            label = label[..maxLabel];

        int gap = ctx.Width - label.Length - value.Length;
        if (gap < 1) gap = 1;
        string line = label + new string(' ', gap) + value;
        WriteText(ms, line, ctx);
    }

    private static void WriteDashLine(MemoryStream ms, ReceiptContext ctx)
        => WriteText(ms, new string('-', ctx.Width), ctx);

    private static void WriteDoubleLine(MemoryStream ms, ReceiptContext ctx)
        => WriteText(ms, new string('=', ctx.Width), ctx);

    private static List<string> WordWrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (word.Length > maxWidth)
                {
                    for (int i = 0; i < word.Length; i += maxWidth)
                        lines.Add(word.Substring(i,
                            Math.Min(maxWidth, word.Length - i)));
                    continue;
                }
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }

    private static decimal GetEffectiveUnitPrice(InvoiceLine ln, PriceMode mode)
        => mode == PriceMode.TTC ? ln.UnitPriceTTC : ln.UnitPriceHT;

    private static string GroupLabel(TaxGroup g, TaxGroupAType? aType)
    {
        if (g == TaxGroup.A && aType == TaxGroupAType.HorsChamp) return "A-HC";
        return ((char)('A' + (int)g)).ToString();
    }

    private static string GetTypeLabel(InvoiceType type) => type switch
    {
        InvoiceType.FV => "Facture de Vente",
        InvoiceType.FT => "Facture d'acompte",
        InvoiceType.EV => "Vente a l'exportation",
        InvoiceType.ET => "Acompte a l'exportation",
        InvoiceType.FA => "Facture d'avoir",
        InvoiceType.EA => "Avoir a l'exportation",
        _ => type.ToString()
    };

    private static string GetPaymentLabel(PaymentType pt) => pt switch
    {
        PaymentType.Especes => "Especes",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Cheque",
        PaymentType.Credit => "Credit",
        _ => pt.ToString()
    };
}