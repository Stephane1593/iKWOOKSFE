using System;

namespace SFE.WPF.Services
{
    /// <summary>
    /// Simple barcode scanner service for keyboard-emulating (HID) scanners.
    /// </summary>
    public interface IBarcodeScannerService
    {
        /// <summary>Raised when a complete barcode payload is detected.</summary>
        event Action<string>? CodeScanned;

        /// <summary>Start listening for scans. Multiple Start calls are idempotent.</summary>
        void Start();

        /// <summary>Stop listening. Multiple Stop calls are idempotent.</summary>
        void Stop();
    }
}