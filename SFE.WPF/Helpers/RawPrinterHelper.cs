using System.Runtime.InteropServices;

namespace SFE.WPF.Helpers;

public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    // ── P/Invoke declarations (unchanged) ──────────────────────────
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA",
        SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter",
        SetLastError = true, ExactSpelling = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA",
        SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level,
        [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter",
        SetLastError = true, ExactSpelling = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter",
        SetLastError = true, ExactSpelling = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter",
        SetLastError = true, ExactSpelling = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter",
        SetLastError = true, ExactSpelling = true)]
    private static extern bool WritePrinter(
        IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Sends raw ESC/POS bytes to the named Windows printer.
    /// Throws on failure with Win32 error detail.
    /// </summary>
    public static void SendBytesToPrinter(
        string printerName, byte[] data, string docName = "SFE Receipt")
    {
        if (!OpenPrinter(printerName.Normalize(), out IntPtr hPrinter, IntPtr.Zero))
            throw new InvalidOperationException(
                $"Cannot open printer \"{printerName}\". " +
                $"Win32 error: {Marshal.GetLastWin32Error()}");
        try
        {
            if (!StartDocPrinter(hPrinter, 1,
                    new DOCINFOA { pDocName = docName, pDataType = "RAW" }))
                throw new InvalidOperationException(
                    $"StartDocPrinter failed. Win32 error: {Marshal.GetLastWin32Error()}");
            try
            {
                if (!StartPagePrinter(hPrinter))
                    throw new InvalidOperationException(
                        $"StartPagePrinter failed. Win32 error: {Marshal.GetLastWin32Error()}");
                try
                {
                    IntPtr pUnmanaged = Marshal.AllocCoTaskMem(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, pUnmanaged, data.Length);
                        if (!WritePrinter(hPrinter, pUnmanaged, data.Length, out _))
                            throw new InvalidOperationException(
                                $"WritePrinter failed. Win32 error: {Marshal.GetLastWin32Error()}");
                    }
                    finally { Marshal.FreeCoTaskMem(pUnmanaged); }
                }
                finally { EndPagePrinter(hPrinter); }   // ✅ always
            }
            finally { EndDocPrinter(hPrinter); }         // ✅ always
        }
        finally { ClosePrinter(hPrinter); }              // ✅ always
    }
}