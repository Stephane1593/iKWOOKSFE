using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WinTimer = System.Timers.Timer;  // alias to avoid ambiguity
using System.Timers;                    // for ElapsedEventArgs

namespace SFE.WPF.Services
{
    /// <summary>
    /// Detects barcode scans from keyboard-emulating USB scanners.
    /// Heuristic: characters arriving fast (< TimeoutMs between chars) are grouped,
    /// Enter (Return) or timeout ends the scan and raises CodeScanned.
    /// </summary>
    public class KeyboardBarcodeScanner : IBarcodeScannerService, IDisposable
    {
        public event Action<string>? CodeScanned;

        private readonly StringBuilder _buf = new();
        private readonly WinTimer _timer;
        private bool _running;

        // If the inter-character delay exceeds this, we treat the buffer as finished.
        private const double TimeoutMs = 80; // tuning: 60..120ms works for most scanners

        public KeyboardBarcodeScanner()
        {
            _timer = new WinTimer(TimeoutMs) { AutoReset = false };
            _timer.Elapsed += Timer_Elapsed;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            // Global input: capture all pre-process input (works across windows/dialogs)
            InputManager.Current.PreProcessInput += InputManager_PreProcessInput;
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            InputManager.Current.PreProcessInput -= InputManager_PreProcessInput;
            _timer.Stop();
            _buf.Clear();
        }

        private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!_running) return;
            var input = e.StagingItem.Input;

            // Text input (characters)
            if (input is TextCompositionEventArgs tcea)
            {
                var txt = tcea.Text ?? "";
                if (!string.IsNullOrEmpty(txt))
                {
                    _buf.Append(txt);
                    RestartTimer();
                }
            }
            // Key events (for Enter/Return)
            else if (input is KeyEventArgs kea)
            {
                if (kea.Key == Key.Enter || kea.Key == Key.Return)
                {
                    var code = _buf.ToString();
                    _buf.Clear();
                    _timer.Stop();

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CodeScanned?.Invoke(code.Trim());
                        }));
                    }
                }
            }
        }

        private void RestartTimer()
        {
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_buf.Length == 0) return;
            var code = _buf.ToString();
            _buf.Clear();
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                CodeScanned?.Invoke(code.Trim());
            }));
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
            GC.SuppressFinalize(this);   // fixes CA1816
        }
    }
}