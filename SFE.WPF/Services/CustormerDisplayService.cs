using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using SFE.WPF.ViewModels;
using SFE.WPF.Views;
using SFE.WPF.Views.Pages;

namespace SFE.WPF.Services;

/// <summary>
/// Manages the customer-facing display window on the secondary screen.
/// Singleton — created at startup, survives across page navigations.
/// </summary>
public class CustomerDisplayService : IDisposable
{
    private CustomerDisplayWindow? _window;
    private CustomerDisplayViewModel? _viewModel;

    public CustomerDisplayViewModel? ViewModel => _viewModel;
    public bool IsOpen => _window != null && _window.IsVisible;

    /// <summary>
    /// Opens the customer display on the secondary monitor (if available).
    /// Safe to call multiple times — only opens once.
    /// </summary>
    public void Open(string companyName)
    {
        if (IsOpen) return;

        var secondary = FindSecondaryMonitor();
        if (secondary == null) return;

        _viewModel = new CustomerDisplayViewModel { CompanyName = companyName };

        _window = new CustomerDisplayWindow
        {
            DataContext = _viewModel,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = secondary.Value.Left,
            Top = secondary.Value.Top,
            Width = secondary.Value.Width,
            Height = secondary.Value.Height,
            WindowState = WindowState.Maximized
        };

        _window.Closed += (_, _) => { _window = null; _viewModel = null; };
        _window.Show();
        _viewModel.SetIdle();
    }

    public void UpdateCart(IEnumerable<CartItemViewModel> items,
        decimal grandTotal, string label, int count)
    {
        _viewModel?.UpdateCart(items, grandTotal, label, count);
    }

    public void ShowNormalized(decimal total, string codeDEFDGI, string? qrContent)
    {
        _viewModel?.ShowNormalized(total, codeDEFDGI, qrContent);
    }

    public void SetIdle() => _viewModel?.SetIdle();

    public void Close()
    {
        _window?.Close();
        _window = null;
        _viewModel = null;
    }

    public void Dispose() => Close();

    // ── Multi-monitor detection via Win32 ──────────────────────────

    private static Rect? FindSecondaryMonitor()
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
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);

        var secondary = monitors.FirstOrDefault(m => !m.IsPrimary);
        if (secondary == null) return null;

        // Convert device pixels → WPF DIPs
        double dpiScale = GetDpiScale();
        return new Rect(
            secondary.WorkArea.Left / dpiScale,
            secondary.WorkArea.Top / dpiScale,
            (secondary.WorkArea.Right - secondary.WorkArea.Left) / dpiScale,
            (secondary.WorkArea.Bottom - secondary.WorkArea.Top) / dpiScale
        );
    }

    private static double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
        return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    // ── P/Invoke declarations ──────────────────────────────────────

    private const int MONITORINFOF_PRIMARY = 0x00000001;

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

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
        public bool IsPrimary { get; set; }
    }
}