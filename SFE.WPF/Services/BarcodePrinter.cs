using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SFE.Domain.Entities;
using SFE.WPF.Controls;

namespace SFE.WPF.Services
{
    /// <summary>
    /// Helper to print product barcode labels. Uses PrintDialog.PrintVisual
    /// and the existing Code128Barcode control. Thread affinity: call from UI thread.
    /// </summary>
    public static class BarcodePrinter
    {
        /// <summary>
        /// Print a product barcode label.
        /// </summary>
        /// <param name="product">Product - Barcode property used (falls back to Code if empty).</param>
        /// <param name="copies">Number of copies to print (1..n)</param>
        /// <param name="thermal">If true, prints a compact 80mm-style layout; otherwise prints a larger label.</param>
        public static void PrintProductBarcode(Product product, int copies = 1, bool thermal = true)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (copies < 1) copies = 1;

            var value = !string.IsNullOrWhiteSpace(product.Barcode) ? product.Barcode.Trim() : product.Code?.Trim() ?? "";

            // Ask user to pick printer and page settings
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            if (thermal)
            {
                // 80 mm receipt-style printing (compact)
                double paperWidth = dlg.PrintableAreaWidth > 0 ? Math.Min(dlg.PrintableAreaWidth, 302) : 272;

                var panel = BuildThermalLabelVisual(product, value, paperWidth);

                // Force layout since the panel has no parent
                panel.Measure(new Size(paperWidth, double.PositiveInfinity));
                panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height));
                panel.UpdateLayout();

                for (int i = 0; i < copies; i++)
                {
                    dlg.PrintVisual(panel, $"Codebar {product.Name} ({value})");
                }
            }
            else
            {
                // Larger label / full-card printing
                var host = BuildLargeLabelVisual(product, value, dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);

                // If printable area is 0 (some drivers), fall back to A4-ish size
                var width = dlg.PrintableAreaWidth > 0 ? dlg.PrintableAreaWidth : 827; // ~210mm @ 96dpi
                var height = dlg.PrintableAreaHeight > 0 ? dlg.PrintableAreaHeight : 1169; // ~297mm

                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0, 0, width, height));
                host.UpdateLayout();

                for (int i = 0; i < copies; i++)
                {
                    dlg.PrintVisual(host, $"Label {product.Name} ({value})");
                }
            }
        }

        private static StackPanel BuildThermalLabelVisual(Product product, string code, double paperWidth)
        {
            var panel = new StackPanel
            {
                Width = paperWidth,
                Background = Brushes.White,
                Margin = new Thickness(0, 6, 0, 6)
            };

            panel.Children.Add(new TextBlock
            {
                Text = product.Name,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Optional price line (show primary currency if present)
            var priceText = "";
            if (product.UnitPriceTtcCdf > 0m)
                priceText = $"{product.UnitPriceTtcCdf:N0} CDF";
            else if (product.UnitPriceTtcUsd > 0m)
                priceText = $"{product.UnitPriceTtcUsd:N2} USD";

            if (!string.IsNullOrEmpty(priceText))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = priceText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }

            var barcode = new Code128Barcode
            {
                Value = code,
                ModuleWidth = 1.2,
                QuietZoneModules = 10,
                BarHeight = 80,
                HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            RenderOptions.SetEdgeMode(barcode, EdgeMode.Aliased);
            RenderOptions.SetBitmapScalingMode(barcode, BitmapScalingMode.NearestNeighbor);
            panel.Children.Add(barcode);

            panel.Children.Add(new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 6, 0, 6)
            });

            return panel;
        }

        private static Grid BuildLargeLabelVisual(Product product, string code, double targetWidth, double targetHeight)
        {
            // Create a centered label scaled to printable area.
            var host = new Grid
            {
                Width = targetWidth > 0 ? targetWidth : 600,
                Height = targetHeight > 0 ? targetHeight : 400,
                Background = Brushes.White
            };

            var inner = new StackPanel
            {
                Width = Math.Min(host.Width - 40, 500),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            inner.Children.Add(new TextBlock
            {
                Text = product.Name,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.Black
            });

            var priceText = "";
            if (product.UnitPriceTtcCdf > 0m)
                priceText = $"{product.UnitPriceTtcCdf:N0} CDF";
            else if (product.UnitPriceTtcUsd > 0m)
                priceText = $"{product.UnitPriceTtcUsd:N2} USD";

            if (!string.IsNullOrEmpty(priceText))
            {
                inner.Children.Add(new TextBlock
                {
                    Text = priceText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12),
                    Foreground = Brushes.Black
                });
            }

            var barcode = new Code128Barcode
            {
                Value = code,
                ModuleWidth = 1.2,        // ⬅ very thin bars
                QuietZoneModules = 10,
                BarHeight = 60,           // ⬅ shorter too (~10mm)
                HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            inner.Children.Add(barcode);

            inner.Children.Add(new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = Brushes.Black
            });

            host.Children.Add(inner);
            return host;
        }

        // Optional helper if you want to rasterize a visual at high DPI (copied approach from ManagerCardWindow).
        public static BitmapSource RenderElementToBitmap(FrameworkElement fe, double dpi = 300)
        {
            var w = Math.Max(1, fe.ActualWidth);
            var h = Math.Max(1, fe.ActualHeight);

            var scale = dpi / 96.0;
            var rtb = new RenderTargetBitmap(
                (int)Math.Ceiling(w * scale),
                (int)Math.Ceiling(h * scale),
                dpi, dpi, PixelFormats.Pbgra32);

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
}