using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SFE.Domain.Entities;
using SFE.WPF.Controls;

namespace SFE.WPF.Views.Windows;

public partial class ManagerCardWindow : Window
{
    private readonly User _user;
    private readonly string _plainCode;

    public ManagerCardWindow(User user, string plainCode)
    {
        InitializeComponent();
        _user = user;
        _plainCode = plainCode;

        TxtFullName.Text = user.FullName;
        TxtRole.Text = user.Role?.Name ?? "—";
        TxtPos.Text = user.PointOfSale != null
            ? $"Point de vente : {user.PointOfSale.Code} — {user.PointOfSale.Name}"
            : "Point de vente : non assigné";
        TxtCode.Text = plainCode;
        TxtIssued.Text = $"Émise le {DateTime.Now:dd/MM/yyyy HH:mm}";
        Barcode.Value = plainCode;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ═════════════════════════════════════════════════════════════════
    // FULL CARD (A4 / standard printer)
    // ═════════════════════════════════════════════════════════════════
    private void OnPrint(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        try
        {
            // 1) Make sure the on-screen card has a valid layout
            CardRoot.UpdateLayout();

            // 2) Rasterize the card at ~300 DPI — no cloning, no reparenting
            var bmp = RenderElementToBitmap(CardRoot, dpi: 300);

            // 3) Wrap the bitmap in an Image sized to fit the printable area
            var img = new Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                Width = dlg.PrintableAreaWidth,
                Height = dlg.PrintableAreaHeight
            };

            var host = new Grid
            {
                Width = dlg.PrintableAreaWidth,
                Height = dlg.PrintableAreaHeight,
                Background = Brushes.White
            };
            host.Children.Add(img);

            host.Measure(new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight));
            host.Arrange(new Rect(0, 0, dlg.PrintableAreaWidth, dlg.PrintableAreaHeight));

            dlg.PrintVisual(host, $"Carte manager — {_user.Username}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible d'imprimer la carte :\n{ex.Message}",
                "Impression", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 80 mm THERMAL RECEIPT (Xprinter) — barcode only, minimalist
    // ═════════════════════════════════════════════════════════════════
    private void OnPrintThermal(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        try
        {
            // 80 mm ≈ 302 DIPs at 96 DPI. Printable width is usually ~72 mm (272 DIP).
            // We take whatever the driver reports, capped to something sane.
            double paperWidth = dlg.PrintableAreaWidth > 0
                ? Math.Min(dlg.PrintableAreaWidth, 302)
                : 272;

            // Build a compact receipt-style visual in code (no reparenting issues)
            var panel = new StackPanel
            {
                Width = paperWidth,
                Background = Brushes.White,
                Margin = new Thickness(0, 6, 0, 6)
            };

            panel.Children.Add(new TextBlock
            {
                Text = "CARTE MANAGER",
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            panel.Children.Add(new TextBlock
            {
                Text = _user.FullName,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextAlignment = TextAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = _user.Role?.Name ?? "—",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Barcode — thick modules print reliably on 203-dpi thermal heads
            var barcode = new Code128Barcode
            {
                Value = _plainCode,
                ModuleWidth = 1.2,        // ⬅ very thin bars
                QuietZoneModules = 10,
                BarHeight = 80,           // ⬅ shorter too (~10mm)
                HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            // ⚠️ IMPORTANT: also make sure the parent container that holds the barcode
            // does NOT have any Stretch, ScaleTransform, or LayoutTransform on it.
            // The barcode must be rendered at its natural size.
            RenderOptions.SetEdgeMode(barcode, EdgeMode.Aliased);
            RenderOptions.SetBitmapScalingMode(barcode, BitmapScalingMode.NearestNeighbor);
            panel.Children.Add(barcode);

            panel.Children.Add(new TextBlock
            {
                Text = _plainCode,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"Émise le {DateTime.Now:dd/MM/yyyy HH:mm}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 6)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "— Ne pas partager —",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Gray
            });

            // Force layout — this panel has no parent, so no InvalidOperationException
            panel.Measure(new Size(paperWidth, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height));
            panel.UpdateLayout();

            dlg.PrintVisual(panel, $"Carte manager (80mm) — {_user.Username}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible d'imprimer sur ticket :\n{ex.Message}",
                "Impression 80 mm", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════
    private static BitmapSource RenderElementToBitmap(FrameworkElement fe, double dpi = 300)
    {
        var w = Math.Max(1, fe.ActualWidth);
        var h = Math.Max(1, fe.ActualHeight);

        var scale = dpi / 96.0;
        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(w * scale),
            (int)Math.Ceiling(h * scale),
            dpi, dpi, PixelFormats.Pbgra32);

        // Draw the element via a VisualBrush — this does NOT reparent it.
        var dv = new DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            var brush = new VisualBrush(fe) { Stretch = Stretch.None };
            ctx.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        }
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}