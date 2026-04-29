using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using SFE.WPF.ViewModels;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.Services;

public class CustomerDisplayService : IDisposable
{
    private CustomerDisplayWindow? _window;
    private CustomerDisplayViewModel? _viewModel;

    public CustomerDisplayViewModel? ViewModel => _viewModel;
    public bool IsOpen => _window != null && _window.IsVisible;

    /// <summary>
    /// Returns a diagnostic string showing all detected monitors.
    /// Use this to debug why the small screen isn't found.
    /// </summary>
    public string DiagnoseScreens()
    {
        var monitors = GetAllMonitors();
        if (monitors.Count == 0)
            return "No monitors detected at all!";

        var lines = new List<string> { $"Detected {monitors.Count} monitor(s):" };
        foreach (var m in monitors)
        {
            int w = m.WorkArea.Right - m.WorkArea.Left;
            int h = m.WorkArea.Bottom - m.WorkArea.Top;
            lines.Add($"  • {(m.IsPrimary ? "PRIMARY" : "SECONDARY")} " +
                       $"— {w}x{h} at ({m.WorkArea.Left},{m.WorkArea.Top})");
        }

        var secondary = monitors.FirstOrDefault(m => !m.IsPrimary);
        if (secondary == null)
            lines.Add("⚠ NO secondary monitor found! Check: Settings → Display → Extend these displays");
        else
            lines.Add("✓ Secondary monitor found — customer display should work");

        return string.Join(Environment.NewLine, lines);
    }

    public void Open(string companyName)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (IsOpen)
            {
                _viewModel!.CompanyName = companyName;
                return;
            }

            _viewModel = new CustomerDisplayViewModel { CompanyName = companyName };

            _window = new CustomerDisplayWindow
            {
                DataContext = _viewModel,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            _window.Closed += (_, _) => { _window = null; _viewModel = null; };

            // ── Find secondary monitor ──
            var secondary = FindSecondaryMonitor();

            if (secondary != null)
            {
                // Found the customer screen — go fullscreen on it
                _window.WindowStyle = WindowStyle.None;
                _window.ResizeMode = ResizeMode.NoResize;
                _window.Left = secondary.Value.Left;
                _window.Top = secondary.Value.Top;
                _window.Width = secondary.Value.Width;
                _window.Height = secondary.Value.Height;
                _window.Show();
                _window.WindowState = WindowState.Maximized;
            }
            else
            {
                // No secondary screen — show floating window so user can see it
                // This helps during development or when screen is set to "Duplicate"
                _window.WindowStyle = WindowStyle.ToolWindow;
                _window.Width = 500;
                _window.Height = 350;
                _window.Left = SystemParameters.PrimaryScreenWidth - 520;
                _window.Top = 20;
                _window.Show();
            }

            _viewModel.SetIdle();
        });
    }

    public void UpdateCart(IEnumerable<CartItemViewModel> items,
        decimal grandTotal, string label, int count)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _viewModel?.UpdateCart(items, grandTotal, label, count);
        });
    }

    public void ShowNormalized(decimal total, string codeDEFDGI, string? qrContent)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _viewModel?.ShowNormalized(total, codeDEFDGI, qrContent);
        });
    }

    public void SetIdle()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _viewModel?.SetIdle();
        });
    }

    public void Close()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _window?.Close();
            _window = null;
            _viewModel = null;
        });
    }

    public void Dispose() => Close();

    // ══════════════════════════════════════════════════════════
    //  MONITOR DETECTION (Win32 — no WinForms needed)
    // ══════════════════════════════════════════════════════════

    private static Rect? FindSecondaryMonitor()
    {
        var monitors = GetAllMonitors();

        // Find non-primary, prefer smallest (customer displays are small)
        var secondary = monitors
            .Where(m => !m.IsPrimary)
            .OrderBy(m => (m.WorkArea.Right - m.WorkArea.Left) *
                          (m.WorkArea.Bottom - m.WorkArea.Top))
            .FirstOrDefault();

        if (secondary == null) return null;

        double dpi = GetDpiScale();
        return new Rect(
            secondary.WorkArea.Left / dpi,
            secondary.WorkArea.Top / dpi,
            (secondary.WorkArea.Right - secondary.WorkArea.Left) / dpi,
            (secondary.WorkArea.Bottom - secondary.WorkArea.Top) / dpi
        );
    }

    private static List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    monitors.Add(new MonitorInfo
                    {
                        WorkArea = mi.rcWork,
                        Monitor = mi.rcMonitor,
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);

        return monitors;
    }

    private static double GetDpiScale()
    {
        try
        {
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                var source = PresentationSource.FromVisual(mainWindow);
                if (source?.CompositionTarget != null)
                    return source.CompositionTarget.TransformToDevice.M11;
            }
        }
        catch { }

        // Fallback: use Win32 to get system DPI
        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc != IntPtr.Zero)
        {
            try
            {
                int dpi = GetDeviceCaps(hdc, 88); // LOGPIXELSX = 88
                return dpi / 96.0;
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
        }

        return 1.0;
    }

    // ── P/Invoke ──

    private const int MONITORINFOF_PRIMARY = 0x00000001;

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private class MonitorInfo
    {
        public RECT WorkArea { get; set; }
        public RECT Monitor { get; set; }
        public bool IsPrimary { get; set; }
    }
}