using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace SFE.WPF.Controls;

    public class Code128Barcode : Control
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(Code128Barcode),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnAnyChanged));

        public static readonly DependencyProperty ModuleWidthProperty =
            DependencyProperty.Register(nameof(ModuleWidth), typeof(double), typeof(Code128Barcode),
                new FrameworkPropertyMetadata(3.0,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnAnyChanged));

        public static readonly DependencyProperty BarHeightProperty =
            DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(Code128Barcode),
                new FrameworkPropertyMetadata(120.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure, OnAnyChanged));

        public static readonly DependencyProperty QuietZoneModulesProperty =
            DependencyProperty.Register(nameof(QuietZoneModules), typeof(int), typeof(Code128Barcode),
                new FrameworkPropertyMetadata(10,
                    FrameworkPropertyMetadataOptions.AffectsMeasure, OnAnyChanged));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double ModuleWidth
        {
            get => (double)GetValue(ModuleWidthProperty);
            set => SetValue(ModuleWidthProperty, value);
        }

        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }

        public int QuietZoneModules
        {
            get => (int)GetValue(QuietZoneModulesProperty);
            set => SetValue(QuietZoneModulesProperty, value);
        }

        private bool[] _modules;

        static Code128Barcode()
        {
            // No default OS theme — pure custom drawing
        }

        public Code128Barcode()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Background = Brushes.White;
        }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Code128Barcode)d).Encode();
        }

    private void Encode()
    {
        var raw = (Value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(raw))
        {
            _modules = null;
        }
        else
        {
            try
            {
                var writer = new Code128Writer();
                // OneDimensionalCodeWriter.encode(string) returns bool[] directly
                _modules = writer.encode(raw);
            }
            catch
            {
                _modules = null;
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size constraint)
        {
            if (_modules == null || _modules.Length == 0)
                return new Size(0, 0);

            var totalModules = _modules.Length + QuietZoneModules * 2;
            var width = totalModules * ModuleWidth;
            var height = BarHeight;
            return new Size(width, height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_modules == null || _modules.Length == 0) return;

            var totalModules = _modules.Length + QuietZoneModules * 2;
            var width = totalModules * ModuleWidth;
            var height = BarHeight;

            // White background (including quiet zone)
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            var black = Brushes.Black;
            double x = QuietZoneModules * ModuleWidth;

            // Draw contiguous black runs as single rectangles = crisp edges
            int i = 0;
            while (i < _modules.Length)
            {
                if (_modules[i])
                {
                    int runStart = i;
                    while (i < _modules.Length && _modules[i]) i++;
                    int runLen = i - runStart;

                    var rect = new Rect(
                        x + runStart * ModuleWidth,
                        0,
                        runLen * ModuleWidth,
                        height);

                    // Guidelines snap edges to device pixels — no anti-aliasing on bar edges
                    var guidelines = new GuidelineSet();
                    guidelines.GuidelinesX.Add(rect.Left);
                    guidelines.GuidelinesX.Add(rect.Right);
                    dc.PushGuidelineSet(guidelines);
                    dc.DrawRectangle(black, null, rect);
                    dc.Pop();
                }
                else
                {
                    i++;
                }
            }
        }
    }
