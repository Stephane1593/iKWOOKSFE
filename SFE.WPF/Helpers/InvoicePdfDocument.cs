using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SFE.Domain.Entities;
using SFE.Domain.Enums;

namespace SFE.WPF.Helpers;

/// <summary>
/// QuestPDF A4 invoice — fully DGI-2026 §1.2 compliant.
/// Forces a page break every 10 article lines (user requirement).
/// </summary>
public class InvoicePdfDocument : IDocument
{
    private const int LinesPerPage = 10;

    private readonly Invoice _inv;
    private readonly Company? _co;
    private readonly PointOfSale? _pos;
    private readonly decimal _xRate;
    private readonly byte[]? _logo;
    private readonly byte[]? _qrCode;

    /// <summary>1 = ORIGINAL, 2 = DUPLICATA N°1, 3 = DUPLICATA N°2, …</summary>
    private readonly int _printNumber;

    // Culture (fr-FR formatting: "1 234,56")
    private static readonly CultureInfo FR = CultureInfo.GetCultureInfo("fr-FR");

    // Palette
    private const string ColPrimary = "#1565C0";
    private const string ColDark = "#0D47A1";
    private const string ColBg = "#F8F9FA";
    private const string ColStripe = "#FAFBFC";
    private const string ColBorder = "#DEE2E6";
    private const string ColText = "#212121";
    private const string ColMuted = "#757575";
    private const string ColRed = "#C62828";
    private const string ColAmber = "#EF6C00";
    private const string ColTeal = "#00897B";
    private const string ColGrey = "#546E7A";
    private const string ColGreen = "#2E7D32";

    public InvoicePdfDocument(
        Invoice invoice,
        Company? company = null,
        PointOfSale? pos = null,
        decimal exchangeRate = 0,
        byte[]? logoBytes = null,
        byte[]? qrCodeBytes = null,
        int printNumber = 1)
    {
        _inv = invoice ?? throw new ArgumentNullException(nameof(invoice));
        _co = company;
        _pos = pos;
        _xRate = exchangeRate;
        _logo = logoBytes;
        _qrCode = qrCodeBytes;
        _printNumber = printNumber < 1 ? 1 : printNumber;
    }

    /// <summary>True if this rendering is a duplicate (i.e. printNumber ≥ 2).</summary>
    private bool IsDuplicate => !_inv.IsProforma && _printNumber >= 2;

    /// <summary>Banner text for the print marker (ORIGINAL / DUPLICATA N°x).</summary>
    private string PrintMarkerText =>
        _printNumber <= 1 ? "ORIGINAL" : $"DUPLICATA N°{_printNumber - 1}";

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{_inv.Type.DisplayBanner()} {_inv.InvoiceNumber}"
                + (IsDuplicate ? $" — {PrintMarkerText}" : ""),
        Author = _co?.Name ?? "SFE",
        Creator = "GECOM2025 — SFE conforme DGI-RDC 2026"
    };

    // ════════════════════════════════════════════════════════════
    //  COMPOSE
    // ════════════════════════════════════════════════════════════
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(18);
            page.MarginBottom(15);
            page.MarginHorizontal(22);
            page.DefaultTextStyle(ts => ts.FontSize(8).FontColor(ColText));

            page.Header().Element(ComposePageHeader);
            page.Content().Element(ComposeBody);
            page.Footer().Element(ComposePageFooter);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  HEADER (repeats every page)
    // ════════════════════════════════════════════════════════════
    private void ComposePageHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // LEFT — company
                row.RelativeItem(3).Column(left =>
                {
                    if (_logo is { Length: > 0 })
                        left.Item().Height(38).Image(_logo).FitHeight();

                    left.Item().Text(_co?.Name ?? "—").Bold().FontSize(13).FontColor(ColDark);

                    var saleAddr = !string.IsNullOrWhiteSpace(_pos?.Address)
                        ? $"{_pos!.Address}, {_pos.City}".TrimEnd(' ', ',')
                        : !string.IsNullOrWhiteSpace(_co?.Address)
                            ? $"{_co!.Address}, {_co.City}".TrimEnd(' ', ',')
                            : "";
                    if (saleAddr.Length > 0)
                        left.Item().Text(saleAddr).FontSize(7).FontColor(ColMuted);

                    var contactBits = new List<string>();
                    if (!string.IsNullOrWhiteSpace(_co?.Phone)) contactBits.Add($"Tél: {_co.Phone}");
                    if (!string.IsNullOrWhiteSpace(_co?.Email)) contactBits.Add($"Email: {_co.Email}");
                    if (contactBits.Count > 0)
                        left.Item().Text(string.Join("  •  ", contactBits)).FontSize(7).FontColor(ColMuted);

                    var idBits = new List<string>();
                    if (!string.IsNullOrWhiteSpace(_co?.NIF)) idBits.Add($"NIF: {_co.NIF}");
                    if (!string.IsNullOrWhiteSpace(_co?.RCCM)) idBits.Add($"RCCM: {_co.RCCM}");
                    if (!string.IsNullOrWhiteSpace(_co?.ISF)) idBits.Add($"ISF: {_co.ISF}");
                    if (idBits.Count > 0)
                        left.Item().PaddingTop(1).Text(string.Join("  •  ", idBits)).FontSize(7).Bold();
                });

                // RIGHT — type banner + invoice meta
                row.RelativeItem(2).Column(right =>
                {
                    right.Item().AlignRight()
                         .Background(BannerColor(_inv.Type))
                         .Padding(6).PaddingHorizontal(12)
                         .AlignCenter()
                         .Text(_inv.Type.DisplayBanner())
                         .Bold().FontSize(11).FontColor(Colors.White);

                    // Sub-tags
                    var tags = new List<(string txt, string color)>();

                    // ── ORIGINAL / DUPLICATA — only for fiscal invoices ──
                    if (!_inv.IsProforma)
                    {
                        if (_printNumber <= 1)
                            tags.Add(("ORIGINAL", ColGreen));
                        else
                            tags.Add(($"DUPLICATA N°{_printNumber - 1}", ColRed));
                    }

                    if (_inv.IsExport) tags.Add(("EXPORTATION", ColTeal));
                    if (_inv.IsAdvanceInvoice) tags.Add(("D'ACOMPTE", ColAmber));
                    if (_inv.IsProforma) tags.Add(("DOCUMENT NON FISCAL", ColGrey));

                    if (tags.Count > 0)
                    {
                        right.Item().PaddingTop(3).AlignRight().Row(r =>
                        {
                            foreach (var (t, c) in tags)
                            {
                                r.AutoItem().PaddingLeft(3)
                                 .Background(c).Padding(3).PaddingHorizontal(6)
                                 .Text(t).Bold().FontSize(7).FontColor(Colors.White);
                            }
                        });
                    }

                    right.Item().PaddingTop(5).AlignRight()
                         .Text(_inv.InvoiceNumber ?? "—").Bold().FontSize(11).FontColor(ColDark);

                    right.Item().AlignRight()
                         .Text($"Émise le {_inv.CreatedAt:dd/MM/yyyy à HH:mm:ss}")
                         .FontSize(7);

                    // ── Reprint timestamp on duplicates ──
                    if (IsDuplicate)
                    {
                        right.Item().AlignRight()
                             .Text($"Réimprimée le {DateTime.Now:dd/MM/yyyy à HH:mm:ss}")
                             .FontSize(7).Italic().FontColor(ColRed);
                    }

                    right.Item().AlignRight()
                         .Text($"Régime: PRIX {_inv.PriceMode}").FontSize(7).Bold();

                    if (!string.IsNullOrEmpty(_inv.ISF))
                        right.Item().AlignRight().Text($"ISF: {_inv.ISF}").FontSize(7);
                    if (!string.IsNullOrEmpty(_inv.OperatorName))
                        right.Item().AlignRight().Text($"Opérateur: {_inv.OperatorName}").FontSize(7);
                    if (_pos != null && !string.IsNullOrEmpty(_pos.Name))
                        right.Item().AlignRight().Text($"Point de vente: {_pos.Name}").FontSize(7);
                });
            });

            // ─── Client band ───
            col.Item().PaddingTop(5)
               .Border(0.5f).BorderColor(ColBorder)
               .Background(ColBg)
               .Padding(5)
               .Row(row =>
               {
                   row.RelativeItem().Column(c =>
                   {
                       c.Item().Text("CLIENT").FontSize(6).Bold().FontColor(ColMuted);
                       c.Item().Text(text =>
                       {
                           text.Span(ClientTypePrefix(_inv.ClientType)).Bold().FontSize(8).FontColor(ColDark);
                           text.Span("  ");
                           text.Span(string.IsNullOrWhiteSpace(_inv.ClientName)
                                   ? "Client comptoir" : _inv.ClientName).FontSize(9).Bold();
                       });
                   });

                   row.RelativeItem().Column(c =>
                   {
                       c.Item().Text("NIF").FontSize(6).Bold().FontColor(ColMuted);
                       c.Item().Text(string.IsNullOrWhiteSpace(_inv.ClientNIF) ? "—" : _inv.ClientNIF).FontSize(9);
                   });

                   row.RelativeItem(2).Column(c =>
                   {
                       c.Item().Text("ADRESSE / CONTACT").FontSize(6).Bold().FontColor(ColMuted);
                       var bits = new List<string>();
                       if (!string.IsNullOrWhiteSpace(_inv.ClientAddress)) bits.Add(_inv.ClientAddress);
                       if (!string.IsNullOrWhiteSpace(_inv.ClientPhone)) bits.Add($"Tél: {_inv.ClientPhone}");
                       if (!string.IsNullOrWhiteSpace(_inv.ClientEmail)) bits.Add(_inv.ClientEmail);
                       c.Item().Text(bits.Count == 0 ? "—" : string.Join(" • ", bits)).FontSize(8);
                   });
               });

            // ─── Source proforma reference (when this is a converted invoice) ───
            if (_inv.SourceProformaId.HasValue && _inv.SourceProforma != null)
            {
                col.Item().PaddingTop(4)
                   .Background("#ECEFF1")
                   .Border(0.5f).BorderColor(ColGrey)
                   .Padding(4)
                   .Text(text =>
                   {
                       text.Span("Issue de la proforma : ").FontSize(7).Bold();
                       text.Span(_inv.SourceProforma.InvoiceNumber).FontSize(8).Bold().FontColor(ColGrey);
                       text.Span($"  du {_inv.SourceProforma.CreatedAt:dd/MM/yyyy}").FontSize(7);
                   });
            }

            // ─── Credit note reference band (FA/EA) ───
            if (_inv.IsCreditNote)
            {
                col.Item().PaddingTop(4)
                   .Background("#FFEBEE")
                   .Border(0.5f).BorderColor(ColRed)
                   .Padding(4)
                   .Row(r =>
                   {
                       r.RelativeItem().Text(text =>
                       {
                           text.Span("Nature : ").FontSize(7).Bold();
                           text.Span(CreditNatureLabel(_inv.CreditNoteNature)).FontSize(8).Bold().FontColor(ColRed);
                       });
                       r.RelativeItem().Text(text =>
                       {
                           text.Span("Référence facture originale : ").FontSize(7).Bold();
                           text.Span(_inv.OriginalInvoiceReference ?? "—").FontSize(8).Bold().FontColor(ColRed);
                       });
                   });
            }

            col.Item().PaddingBottom(3);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  BODY
    // ════════════════════════════════════════════════════════════
    private void ComposeBody(IContainer container)
    {
        container.Column(col =>
        {
            var lines = _inv.Lines?.OrderBy(l => l.LineNumber).ToList() ?? new();
            bool hasDiscount = lines.Any(l => l.DiscountAmount > 0);
            bool hasTS = lines.Any(l => l.TaxSpecificAmount > 0);

            var chunks = lines.Chunk(LinesPerPage).ToList();
            if (chunks.Count == 0) chunks.Add(Array.Empty<InvoiceLine>());

            for (int pageIdx = 0; pageIdx < chunks.Count; pageIdx++)
            {
                bool isLast = pageIdx == chunks.Count - 1;
                col.Item().Element(c => RenderItemsTable(c, chunks[pageIdx], hasDiscount, hasTS,
                                                         pageIdx + 1, chunks.Count));

                if (!isLast)
                {
                    col.Item().AlignRight().PaddingTop(2)
                       .Text($"… suite page {pageIdx + 2}/{chunks.Count}")
                       .FontSize(7).Italic().FontColor(ColMuted);
                    col.Item().PageBreak();
                }
            }

            col.Item().PaddingTop(4)
               .Text($"Nombre d'articles : {lines.Count}").Bold().FontSize(8);

            col.Item().PaddingTop(5).ShowEntire().Element(ComposeFiscalSummary);

            if (_inv.IsAdvanceInvoice)
                col.Item().PaddingTop(5).ShowEntire().Element(ComposeAdvanceBlock);
            else if (_inv.IsFinalWithAdvances)
                col.Item().PaddingTop(5).ShowEntire().Element(ComposeFinalAdvanceBlock);

            col.Item().PaddingTop(5).ShowEntire().Element(ComposeAmountInWords);
            col.Item().PaddingTop(5).ShowEntire().Element(ComposeCommentsAndPayments);

            if (!_inv.IsProforma && !string.IsNullOrEmpty(_inv.CodeDEFDGI))
                col.Item().PaddingTop(6).ShowEntire().Element(ComposeSecurityBlock);
            else if (_inv.IsProforma)
                col.Item().PaddingTop(8).ShowEntire().Element(ComposeProformaNotice);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  ITEMS TABLE
    // ════════════════════════════════════════════════════════════
    private void RenderItemsTable(
        IContainer container,
        IReadOnlyList<InvoiceLine> lines,
        bool hasDiscount, bool hasTS,
        int pageNum, int pageCount)
    {
        container.Column(c =>
        {
            if (pageCount > 1)
            {
                c.Item().AlignRight().Text($"Articles — page {pageNum}/{pageCount}")
                 .FontSize(7).Italic().FontColor(ColMuted);
            }

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(16);
                    cols.ConstantColumn(50);
                    cols.RelativeColumn();
                    cols.ConstantColumn(46);
                    cols.ConstantColumn(54);
                    cols.ConstantColumn(38);
                    cols.ConstantColumn(28);
                    if (hasDiscount) cols.ConstantColumn(48);
                    if (hasTS) cols.ConstantColumn(46);
                    cols.ConstantColumn(58);
                    cols.ConstantColumn(48);
                    cols.ConstantColumn(60);
                });

                table.Header(h =>
                {
                    void H(string t, bool right = false)
                    {
                        var cell = h.Cell().Background(ColPrimary)
                                    .PaddingHorizontal(3).PaddingVertical(3);
                        var dst = right ? cell.AlignRight() : cell;
                        dst.Text(t).FontSize(6.5f).Bold().FontColor(Colors.White);
                    }
                    H("#"); H("Code"); H("Désignation"); H("Grp/Type");
                    H("P.U.", true); H("Qté", true); H("Unité");
                    if (hasDiscount) H("Remise", true);
                    if (hasTS) H("T.S.", true);
                    H("H.T.", true); H("TVA", true); H("TTC", true);
                });

                for (int i = 0; i < lines.Count; i++)
                {
                    var ln = lines[i];
                    var bg = i % 2 == 1 ? ColStripe : Colors.White.ToString();

                    void Cb(string t, bool right = false, bool bold = false)
                    {
                        var cell = table.Cell().Background(bg)
                                    .BorderBottom(0.5f).BorderColor(ColBorder)
                                    .PaddingHorizontal(3).PaddingVertical(2);
                        var dst = right ? cell.AlignRight() : cell;
                        var span = dst.Text(t).FontSize(7);
                        if (bold) span.Bold();
                    }

                    decimal unitPrice = _inv.PriceMode == PriceMode.TTC
                        ? ln.UnitPriceTTC : ln.UnitPriceHT;

                    Cb(ln.LineNumber.ToString());
                    Cb(ln.Code ?? "");
                    Cb(ln.Name ?? "");
                    Cb($"[{ln.TaxGroup}][{ln.ItemType}]");
                    Cb(M(unitPrice), right: true);
                    Cb(Q(ln.Quantity), right: true);
                    Cb(ln.Unit ?? "pce");
                    if (hasDiscount)
                        Cb(ln.DiscountAmount > 0 ? FormatDiscount(ln) : "", right: true);
                    if (hasTS)
                        Cb(ln.TaxSpecificAmount > 0 ? M(ln.TaxSpecificAmount) : "", right: true);
                    Cb(M(ln.AmountHT), right: true);
                    Cb(M(ln.AmountTVA), right: true);
                    Cb(M(ln.AmountTTC), right: true, bold: true);
                }
            });
        });
    }

    private static string FormatDiscount(InvoiceLine ln) => ln.DiscountType switch
    {
        DiscountType.Percentage => $"-{Q(ln.DiscountValue)}% ({M(ln.DiscountAmount)})",
        DiscountType.FixedAmount => $"-{M(ln.DiscountAmount)}",
        _ => ""
    };

    // ════════════════════════════════════════════════════════════
    //  FISCAL SUMMARY (unchanged)
    // ════════════════════════════════════════════════════════════
    private void ComposeFiscalSummary(IContainer container)
    {
        var lines = _inv.Lines?.ToList() ?? new();

        var groups = lines
            .GroupBy(l => l.TaxGroup)
            .OrderBy(g => g.Key)
            .Select(g => new {
                Group = g.Key,
                Rate = g.First().TaxRate,
                HT = g.Sum(l => l.AmountHT - l.TaxSpecificAmount),
                TS = g.Sum(l => l.TaxSpecificAmount),
                TVA = g.Sum(l => l.AmountTVA),
                TTC = g.Sum(l => l.AmountTTC)
            })
            .ToList();

        container.Row(row =>
        {
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text("RÉCAPITULATIF FISCAL PAR GROUPE")
                   .Bold().FontSize(8).FontColor(ColDark);

                col.Item().PaddingTop(2).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);
                        c.RelativeColumn();
                        c.ConstantColumn(32);
                        c.ConstantColumn(60);
                        c.ConstantColumn(50);
                        c.ConstantColumn(55);
                        c.ConstantColumn(60);
                    });

                    t.Header(h =>
                    {
                        void H(string txt, bool right = false)
                        {
                            var cell = h.Cell().Background(ColBg)
                                .BorderBottom(0.5f).BorderColor(ColBorder)
                                .Padding(3);
                            var dst = right ? cell.AlignRight() : cell;
                            dst.Text(txt).FontSize(6.5f).Bold().FontColor(ColMuted);
                        }
                        H("Grp"); H("Description");
                        H("Taux", true); H("Base HT", true);
                        H("T.S.", true); H("TVA", true); H("TTC", true);
                    });

                    foreach (var g in groups)
                    {
                        void C(string txt, bool right = false)
                        {
                            var cell = t.Cell()
                                .BorderBottom(0.3f).BorderColor(ColBorder)
                                .Padding(2.5f);
                            var dst = right ? cell.AlignRight() : cell;
                            dst.Text(txt).FontSize(7);
                        }
                        C(g.Group.ToString());
                        C(GroupLabel(g.Group));
                        C($"{R(g.Rate)} %", right: true);
                        C(M(g.HT), right: true);
                        C(g.TS > 0 ? M(g.TS) : "—", right: true);
                        C(g.TVA > 0 ? M(g.TVA) : "—", right: true);
                        C(M(g.TTC), right: true);
                    }
                });
            });

            row.ConstantItem(10);

            row.RelativeItem(2).Column(col =>
            {
                Total(col, "Total HT (avant remise)", _inv.TotalHTBeforeDiscount, false);
                if (_inv.TotalDiscount > 0)
                    Total(col, "Remise globale", -_inv.TotalDiscount, false, ColRed);
                Total(col, "Total H.T. net", _inv.TotalHT, false);
                if (_inv.TotalSpecificTax > 0)
                    Total(col, "Total Taxes Spécifiques", _inv.TotalSpecificTax, false);
                Total(col, "Total T.V.A.", _inv.TotalTVA, false);

                col.Item().PaddingTop(4).Background(ColPrimary).Padding(7).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL TTC (CDF)")
                     .FontSize(11).Bold().FontColor(Colors.White);
                    r.ConstantItem(110).AlignRight()
                     .Text(M(_inv.TotalTTC))
                     .FontSize(13).Bold().FontColor(Colors.White);
                });

                if (_xRate > 0)
                {
                    var usd = Math.Round(_inv.TotalTTC / _xRate, 2);
                    col.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text($"Taux de change (BCC)").FontSize(7);
                        r.ConstantItem(110).AlignRight()
                         .Text($"1 USD = {X(_xRate)} CDF").FontSize(7).Bold();
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Équivalent USD").FontSize(8);
                        r.ConstantItem(110).AlignRight()
                         .Text($"{M(usd)} USD").FontSize(9).Bold().FontColor(ColTeal);
                    });
                }
            });
        });
    }

    private static void Total(ColumnDescriptor col, string label, decimal val,
                              bool bold, string? color = null)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(7.5f);
            var span = r.ConstantItem(110).AlignRight().Text(M(val)).FontSize(7.5f);
            if (bold) span.Bold();
            if (color != null) span.FontColor(color);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  ADVANCE BLOCKS (unchanged)
    // ════════════════════════════════════════════════════════════
    private void ComposeAdvanceBlock(IContainer container)
    {
        container.Background("#FFF8E1").Border(0.5f).BorderColor(ColAmber).Padding(7)
            .Column(col =>
            {
                col.Item().Text("DÉTAIL DE L'ACOMPTE").Bold().FontSize(9).FontColor(ColAmber);
                col.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        AdvLine(c, "Total commande planifiée", _inv.OrderTotal);
                        AdvLine(c, "Cumul acomptes antérieurs", _inv.PreviousAdvancesTotal);
                        AdvLine(c, "Acompte versé (cette facture)", _inv.AdvanceAmount, bold: true);
                        AdvLine(c, "Reste à percevoir", _inv.RemainingAfterAdvance, bold: true, color: ColRed);
                    });
                });
                if (!string.IsNullOrWhiteSpace(_inv.AdvanceGroupId))
                    col.Item().PaddingTop(2)
                       .Text($"Référence projet : {_inv.AdvanceGroupId}")
                       .FontSize(7).Italic().FontColor(ColMuted);
            });
    }

    private void ComposeFinalAdvanceBlock(IContainer container)
    {
        container.Background("#E8F5E9").Border(0.5f).BorderColor(ColTeal).Padding(7)
            .Column(col =>
            {
                col.Item().Text("SOLDE — FACTURE FINALE APRÈS ACOMPTES")
                   .Bold().FontSize(9).FontColor(ColTeal);
                col.Item().PaddingTop(3).Column(c =>
                {
                    AdvLine(c, "Total facturé (TTC)", _inv.TotalTTC);
                    AdvLine(c, "Acomptes déjà perçus", _inv.TotalAdvancesPaid);
                    AdvLine(c, "SOLDE DÛ", _inv.RemainingBalance,
                            bold: true, color: ColRed);
                });
                if (!string.IsNullOrWhiteSpace(_inv.AdvanceGroupId))
                    col.Item().PaddingTop(2)
                       .Text($"Référence projet : {_inv.AdvanceGroupId}")
                       .FontSize(7).Italic().FontColor(ColMuted);
            });
    }

    private static void AdvLine(ColumnDescriptor col, string label, decimal v,
                                bool bold = false, string? color = null)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8);
            var span = r.ConstantItem(120).AlignRight()
                .Text($"{M(v)} CDF").FontSize(9);
            if (bold) span.Bold();
            if (color != null) span.FontColor(color);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  AMOUNT IN WORDS (unchanged)
    // ════════════════════════════════════════════════════════════
    private void ComposeAmountInWords(IContainer container)
    {
        container.BorderTop(0.5f).BorderColor(ColBorder).PaddingTop(4)
            .Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).Italic());
                t.Span("Arrêté la présente facture à la somme de ");
                t.Span(NumberToFrenchWords.Convert(_inv.TotalTTC, "")).Bold();
                t.Span(" francs congolais toutes taxes comprises.");
            });
    }

    // ════════════════════════════════════════════════════════════
    //  COMMENTS + PAYMENTS (unchanged)
    // ════════════════════════════════════════════════════════════
    private void ComposeCommentsAndPayments(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Text("COMMENTAIRES (Annexe IV)")
                   .FontSize(7).Bold().FontColor(ColMuted);

                col.Item().PaddingTop(2).Border(0.5f).BorderColor(ColBorder)
                   .MinHeight(48).Padding(4).Column(inner =>
                   {
                       var rows = new (string id, string val)[]
                       {
                           ("A", _inv.CommentA), ("B", _inv.CommentB),
                           ("C", _inv.CommentC), ("D", _inv.CommentD),
                           ("E", _inv.CommentE), ("F", _inv.CommentF),
                           ("G", _inv.CommentG), ("H", _inv.CommentH),
                       };
                       bool any = false;
                       foreach (var (id, v) in rows)
                       {
                           if (string.IsNullOrWhiteSpace(v)) continue;
                           any = true;
                           inner.Item().Text(t =>
                           {
                               t.Span($"Ligne {id}: ").Bold().FontSize(7);
                               t.Span(v).FontSize(7);
                           });
                       }
                       if (!any)
                           inner.Item().Text("—").FontSize(7).FontColor(ColMuted);
                   });
            });

            row.ConstantItem(10);

            row.RelativeItem(2).Column(col =>
            {
                col.Item().Text("MODES DE PAIEMENT")
                   .FontSize(7).Bold().FontColor(ColMuted);

                col.Item().PaddingTop(2).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.ConstantColumn(95);
                    });

                    var pays = _inv.Payments?.ToList() ?? new();
                    foreach (var p in pays)
                    {
                        t.Cell().BorderBottom(0.3f).BorderColor(ColBorder).Padding(3)
                         .Text(PaymentLabel(p.PaymentType)).FontSize(7);
                        t.Cell().BorderBottom(0.3f).BorderColor(ColBorder).Padding(3)
                         .AlignRight().Text(M(p.Amount)).FontSize(7);
                    }

                    t.Cell().Background(ColBg).Padding(3)
                     .Text("TOTAL").FontSize(8).Bold();
                    t.Cell().Background(ColBg).Padding(3).AlignRight()
                     .Text(M(pays.Sum(p => p.Amount))).FontSize(8).Bold();
                });
            });
        });
    }

    // ════════════════════════════════════════════════════════════
    //  SECURITY BLOCK — now mentions duplicate count
    // ════════════════════════════════════════════════════════════
    private void ComposeSecurityBlock(IContainer container)
    {
        container.BorderTop(1).BorderColor(ColDark).PaddingTop(5).Column(col =>
        {
            col.Item().Text("ÉLÉMENTS DE SÉCURITÉ DE LA FACTURE")
               .Bold().FontSize(8).FontColor(ColDark);

            col.Item().PaddingTop(3).Row(row =>
            {
                if (_qrCode is { Length: > 0 })
                    row.ConstantItem(85).Height(85).Image(_qrCode).FitArea();
                else
                    row.ConstantItem(85).Height(85).Border(0.5f).BorderColor(ColBorder)
                       .AlignCenter().AlignMiddle().Text("[QR]").FontSize(8).FontColor(ColMuted);

                row.ConstantItem(10);

                row.RelativeItem().Column(c =>
                {
                    SecRow(c, "Code DEF/DGI", _inv.CodeDEFDGI);
                    SecRow(c, "DEF/NID (NIM)", _inv.NIM);
                    SecRow(c, "DEF Compteurs", _inv.Counters);
                    if (_inv.NormalizedAt.HasValue)
                        SecRow(c, "DEF Heure",
                            _inv.NormalizedAt.Value.ToString("dd/MM/yyyy HH:mm:ss"));
                    if (!string.IsNullOrEmpty(_inv.EmcfUid))
                        SecRow(c, "MCF UID", _inv.EmcfUid);

                    // 🆕 Print marker
                    SecRow(c, "Tirage", PrintMarkerText);
                });
            });

            col.Item().PaddingTop(3)
               .Text("Cette facture est conforme à la norme DGI-RDC 2026 — facture normalisée.")
               .FontSize(6.5f).Italic().FontColor(ColMuted);
        });
    }

    private static void SecRow(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Row(r =>
        {
            r.ConstantItem(85).Text($"{label} :").FontSize(7).Bold();
            r.RelativeItem().Text(value).FontSize(7).FontColor(ColDark);
        });
    }

    private void ComposeProformaNotice(IContainer container)
    {
        container.Background("#ECEFF1").Border(0.5f).BorderColor(ColGrey).Padding(8)
            .Column(c =>
            {
                c.Item().AlignCenter().Text("DOCUMENT NON FISCAL")
                 .Bold().FontSize(11).FontColor(ColGrey);
                c.Item().AlignCenter().Text(
                    "Cette facture proforma ne tient pas lieu de facture fiscale. " +
                    "Elle ne donne droit ni à déduction ni à crédit de TVA.")
                 .FontSize(7.5f).Italic();
                if (_inv.ProformaValidUntil.HasValue)
                    c.Item().PaddingTop(2).AlignCenter()
                     .Text($"Valable jusqu'au {_inv.ProformaValidUntil:dd/MM/yyyy}")
                     .FontSize(8).Bold();
            });
    }

    // ════════════════════════════════════════════════════════════
    //  FOOTER — adds duplicate marker
    // ════════════════════════════════════════════════════════════
    private void ComposePageFooter(IContainer container)
    {
        container.BorderTop(0.5f).BorderColor(ColBorder).PaddingTop(3)
            .Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(ColMuted));
                    t.Span("GECOM2025 — Système de Facturation Électronique • Conforme DGI-RDC 2026");
                    if (!string.IsNullOrEmpty(_inv.ISF)) t.Span($" • ISF {_inv.ISF}");
                    if (IsDuplicate)
                        t.Span($" • {PrintMarkerText}").FontColor(ColRed);
                });
                row.ConstantItem(80).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(ColMuted));
                    t.Span("Page "); t.CurrentPageNumber();
                    t.Span(" / "); t.TotalPages();
                });
            });
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS (unchanged)
    // ════════════════════════════════════════════════════════════
    private static string M(decimal v) => v.ToString("N2", FR);
    private static string Q(decimal v) => v.ToString("0.###", FR);
    private static string R(decimal v) => v.ToString("0.##", FR);
    private static string X(decimal v) => v.ToString("N4", FR);

    private static string BannerColor(InvoiceType t) => t switch
    {
        InvoiceType.FV => ColPrimary,
        InvoiceType.FT => ColAmber,
        InvoiceType.FA => ColRed,
        InvoiceType.EV => ColTeal,
        InvoiceType.ET => "#EF6C00",
        InvoiceType.EA => "#B71C1C",
        InvoiceType.PRO => ColGrey,
        _ => ColMuted
    };

    private static string ClientTypePrefix(ClientType t) => t switch
    {
        ClientType.PP => "[PP] Personne physique",
        ClientType.PM => "[PM] Personne Morale",
        ClientType.PC => "[PC] Personne physique commerçante",
        ClientType.PL => "[PL] Profession libérale",
        ClientType.AO => "[AO] Ambassades & Organisations internationales",
        _ => $"[{t}]"
    };

    private static string CreditNatureLabel(CreditNoteNature? n) => n switch
    {
        CreditNoteNature.COR => "Correction",
        CreditNoteNature.RAN => "Annulation",
        CreditNoteNature.RAM => "Avoir suite reprise",
        CreditNoteNature.RRR => "Rabais / Remise / Ristourne",
        _ => "—"
    };

    private static string GroupLabel(TaxGroup g) => g switch
    {
        TaxGroup.A => "Exonéré / Hors champ",
        TaxGroup.B => "Taxable 16 %",
        TaxGroup.C => "Taxable 5 %",
        TaxGroup.D => "Régime dérogatoire TVA",
        TaxGroup.E => "Exportation",
        TaxGroup.F => "Marché public ext. 16 %",
        TaxGroup.G => "Marché public ext. 5 %",
        TaxGroup.H => "Consignation/déconsignation",
        TaxGroup.I => "Garantie / caution",
        TaxGroup.J => "Débours",
        TaxGroup.K => "Non-assujettis",
        TaxGroup.L => "Prélèvement sur vente",
        TaxGroup.M => "Vente régl. (TVA spécifique)",
        TaxGroup.N => "TVA spécifique",
        TaxGroup.O => "Taxable 1 %",
        TaxGroup.P => "Marché public ext. 1 %",
        _ => g.ToString()
    };

    private static string PaymentLabel(PaymentType p) => p switch
    {
        PaymentType.Especes => "Espèces",
        PaymentType.Virement => "Virement",
        PaymentType.CarteBancaire => "Carte bancaire",
        PaymentType.MobileMoney => "Mobile Money",
        PaymentType.Cheques => "Chèque",
        PaymentType.Credit => "Crédit",
        _ => p.ToString()
    };
}