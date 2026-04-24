using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SFE.WPF.Views.Pages;
using SFE.WPF.ViewModels;
using Size = System.Windows.Size;

namespace SFE.WPF.Helpers;

public static class InvoicePrintHelper
{
    private const double A4WidthDips = 793.7;   // 210 mm @ 96 DPI
    private const double A4HeightDips = 1122.5;  // 297 mm @ 96 DPI
    private const double PageMarginDips = 40;
    private const double ExportDpi = 288;     // 3× for sharp PDF output
    private const double MinRenderWidth = 800;     // safety floor (DIPs)

    // ═══════════════════════════════════════════════════════
    //  PUBLIC — Print  (temp PDF → system viewer)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Generates a temporary PDF and opens it in the default PDF viewer
    /// so the user gets a full preview and can print from there.
    /// </summary>
    public static void Print(InvoiceDocumentViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        string tempDir = Path.Combine(Path.GetTempPath(), "SFE_Invoices");
        Directory.CreateDirectory(tempDir);
        CleanTempFiles(tempDir, TimeSpan.FromDays(1));

        string tempPath = Path.Combine(tempDir,
            SanitizeFileName(
                $"Facture_{viewModel.InvoiceNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"));

        GeneratePdf(viewModel, tempPath);

        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
    }

    // ═══════════════════════════════════════════════════════
    //  PUBLIC — Export  (SaveFileDialog → PDF or PNG)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Shows a Save dialog, exports, then opens the resulting file.
    /// Returns <c>true</c> if the user confirmed; <c>false</c> on cancel.
    /// </summary>
    public static bool ExportPdf(InvoiceDocumentViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var dlg = new SaveFileDialog
        {
            Title = "Exporter la facture",
            FileName = SanitizeFileName($"Facture_{viewModel.InvoiceNumber}"),
            Filter = "Document PDF|*.pdf|Image PNG|*.png",
            DefaultExt = ".pdf"
        };

        if (dlg.ShowDialog() != true)
            return false;                       // user cancelled

        string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

        if (ext == ".png")
            ExportAsPng(viewModel, dlg.FileName);
        else
            GeneratePdf(viewModel, dlg.FileName);

        // Open so the user can verify immediately
        Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        return true;
    }

    // ═══════════════════════════════════════════════════════
    //  PUBLIC — Generate PDF to a given path (reusable)
    // ═══════════════════════════════════════════════════════

    public static void GeneratePdf(InvoiceDocumentViewModel viewModel, string outputPath)
    {
        // ── 1. Render view at its NATURAL width (nothing clipped) ──
        var view = CreateRenderedView(viewModel);

        double viewW = view.ActualWidth;
        double viewH = view.ActualHeight;

        if (viewW < 1 || viewH < 1)
            throw new InvalidOperationException(
                "Le document est vide — impossible de générer le PDF.");

        // ── 2. Page-slice arithmetic ──
        //   We will scale each image to fit A4 width (FitWidth),
        //   so we must compute how much view-height fits per page
        //   at that scale.
        double a4ContentW = A4WidthDips - 2 * PageMarginDips;
        double a4ContentH = A4HeightDips - 2 * PageMarginDips;
        double fitScale = a4ContentW / viewW;          // < 1 when view is wider than A4
        double pageSliceH = a4ContentH / fitScale;        // view-DIPs of height per page

        int pageCount = Math.Max(1, (int)Math.Ceiling(viewH / pageSliceH));
        double dpiScale = ExportDpi / 96.0;

        // ── 3. Render each slice to a PNG byte[] ──
        var pageImages = new List<byte[]>(pageCount);

        for (int i = 0; i < pageCount; i++)
        {
            double yOffset = i * pageSliceH;
            double sliceH = Math.Min(pageSliceH, viewH - yOffset);

            int pxW = (int)Math.Ceiling(viewW * dpiScale);
            int pxH = (int)Math.Ceiling(sliceH * dpiScale);
            if (pxW < 1 || pxH < 1) continue;

            var rtb = new RenderTargetBitmap(
                pxW, pxH, ExportDpi, ExportDpi, PixelFormats.Pbgra32);

            // White background
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
                dc.DrawRectangle(Brushes.White, null,
                    new Rect(0, 0, viewW, sliceH));
            rtb.Render(bg);

            // Render the correct vertical slice of the view
            var slice = new DrawingVisual();
            using (var dc = slice.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(0, -yOffset));
                dc.DrawRectangle(
                    new VisualBrush(view)
                    {
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top
                    },
                    null,
                    new Rect(0, 0, viewW, viewH));
                dc.Pop();
            }
            rtb.Render(slice);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pageImages.Add(ms.ToArray());
        }

        // ── 4. Build PDF — FitWidth scales each image to the page ──
        Document.Create(container =>
        {
            foreach (byte[] imageBytes in pageImages)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin((float)(PageMarginDips * 0.75));   // DIPs → points
                    page.Content()
                        .Image(imageBytes)
                        .FitWidth();                                // ← KEY FIX
                });
            }
        })
        .GeneratePdf(outputPath);
    }

    // ═══════════════════════════════════════════════════════
    //  PNG EXPORT
    // ═══════════════════════════════════════════════════════

    private static void ExportAsPng(InvoiceDocumentViewModel viewModel, string filePath)
    {
        var view = CreateRenderedView(viewModel);

        const double dpi = 192;
        double scale = dpi / 96.0;

        int pxW = (int)Math.Ceiling(view.ActualWidth * scale);
        int pxH = (int)Math.Ceiling(view.ActualHeight * scale);

        if (pxW < 1 || pxH < 1)
            throw new InvalidOperationException(
                "Le document est vide — impossible d'exporter en PNG.");

        var rtb = new RenderTargetBitmap(pxW, pxH, dpi, dpi, PixelFormats.Pbgra32);

        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(Brushes.White, null,
                new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        rtb.Render(bg);
        rtb.Render(view);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.Create(filePath);
        encoder.Save(fs);
    }

    // ═══════════════════════════════════════════════════════
    //  CORE — create view at NATURAL width (no clipping)
    // ═══════════════════════════════════════════════════════

    private static InvoiceDocumentView CreateRenderedView(
        InvoiceDocumentViewModel vm)
    {
        var view = new InvoiceDocumentView { DataContext = vm };

        // Ensure opaque background
        if (view.Background == null
            || view.Background == Brushes.Transparent
            || (view.Background is SolidColorBrush scb && scb.Color.A == 0))
        {
            view.Background = Brushes.White;
        }

        // ── Pass 1 — measure unconstrained to discover natural width ──
        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double naturalW = Math.Max(view.DesiredSize.Width, MinRenderWidth);
        double naturalH = Math.Max(view.DesiredSize.Height, 1);

        view.Arrange(new Rect(0, 0, naturalW, naturalH));
        view.UpdateLayout();
        FlushDispatcher();

        // ── Pass 2 — re-measure at discovered width (for wrapping) ──
        view.Measure(new Size(naturalW, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0,
            view.DesiredSize.Width, view.DesiredSize.Height));
        view.UpdateLayout();
        FlushDispatcher();

        // ── Pass 3 — final settle ──
        view.Measure(new Size(view.ActualWidth, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        view.UpdateLayout();

        return view;
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    private static void FlushDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.SystemIdle,
            new DispatcherOperationCallback(f =>
            {
                ((DispatcherFrame)f!).Continue = false;
                return null;
            }),
            frame);
        Dispatcher.PushFrame(frame);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    /// <summary>Delete temp PDFs older than <paramref name="maxAge"/>.</summary>
    private static void CleanTempFiles(string directory, TimeSpan maxAge)
    {
        try
        {
            var cutoff = DateTime.Now - maxAge;
            foreach (var file in Directory.GetFiles(directory, "*.pdf"))
            {
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* non-critical */ }
    }
}