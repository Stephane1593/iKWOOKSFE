using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinTimer = System.Timers.Timer;
using System.Timers;

namespace SFE.WPF.Services
{
    public class KeyboardBarcodeScanner : IBarcodeScannerService, IDisposable
    {
        public event Action<string>? CodeScanned;

        private readonly StringBuilder _buf = new();
        private readonly WinTimer _timer;
        private bool _running;
        private int _pauseCount;
        private DateTime _lastKeyUtc = DateTime.MinValue;

        private const double InterCharMaxMs = 50;
        private const double FlushTimeoutMs = 80;
        private const double MaxBufferAgeMs = 1500;
        private const int MinScanLength = 4;

        public KeyboardBarcodeScanner()
        {
            _timer = new WinTimer(FlushTimeoutMs) { AutoReset = false };
            _timer.Elapsed += Timer_Elapsed;
        }

        public bool IsPaused => _pauseCount > 0;

        public IDisposable Pause()
        {
            System.Threading.Interlocked.Increment(ref _pauseCount);
            _buf.Clear();
            _timer.Stop();
            return new PauseToken(this);
        }

        private void ResumeInternal()
        {
            System.Threading.Interlocked.Decrement(ref _pauseCount);
            if (_pauseCount < 0) _pauseCount = 0;
        }

        private sealed class PauseToken : IDisposable
        {
            private KeyboardBarcodeScanner? _owner;
            public PauseToken(KeyboardBarcodeScanner o) => _owner = o;
            public void Dispose()
            {
                var o = System.Threading.Interlocked.Exchange(ref _owner, null);
                o?.ResumeInternal();
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            System.Diagnostics.Debug.WriteLine("[SCAN] Start");
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            _timer.Stop();
            _buf.Clear();
            System.Diagnostics.Debug.WriteLine("[SCAN] Stop");
        }

        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!_running || IsPaused) return;

            // NOTE: We intentionally do NOT check ComponentDispatcher.IsThreadModal here.
            // Subscribers (PosViewModel, ManagerAuthorizationPrompter) decide themselves
            // whether to act on a scan based on their own context.


            if (e.StagingItem.Input is not KeyEventArgs kea) return;
            if (kea.RoutedEvent != Keyboard.PreviewKeyDownEvent) return;

            // Self-healing: if we have leftover chars from an interrupted scan,
            // drop them before starting a new one.
            if (_buf.Length > 0 &&
                (DateTime.UtcNow - _lastKeyUtc).TotalMilliseconds > MaxBufferAgeMs)
            {
                _buf.Clear();
            }

            var key = kea.Key == Key.System ? kea.SystemKey : kea.Key;

            if (key == Key.Enter || key == Key.Return)
            {
                if (_buf.Length >= MinScanLength)
                {
                    var code = _buf.ToString().Trim();
                    _buf.Clear();
                    _timer.Stop();
                    Fire(code);
                    kea.Handled = true;
                }
                else
                {
                    _buf.Clear();
                    _timer.Stop();
                }
                return;
            }

            var ch = KeyToChar(key, Keyboard.Modifiers);
            if (ch == null) return;

            var now = DateTime.UtcNow;
            var delta = (now - _lastKeyUtc).TotalMilliseconds;
            _lastKeyUtc = now;

            if (_buf.Length > 0 && delta > InterCharMaxMs)
                _buf.Clear();

            _buf.Append(ch.Value);
            RestartTimer();
        }

        private static char? KeyToChar(Key key, ModifierKeys mods)
        {
            bool shift = (mods & ModifierKeys.Shift) != 0;

            if (key >= Key.D0 && key <= Key.D9 && !shift)
                return (char)('0' + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (char)('0' + (key - Key.NumPad0));
            if (key >= Key.A && key <= Key.Z)
                return shift ? (char)('A' + (key - Key.A))
                             : (char)('a' + (key - Key.A));

            return key switch
            {
                Key.OemMinus or Key.Subtract => '-',
                Key.OemPeriod or Key.Decimal => '.',
                Key.OemComma => ',',
                Key.Space => ' ',
                Key.Divide or Key.OemQuestion => '/',
                Key.OemPlus or Key.Add => '+',
                _ => null
            };
        }

        private void RestartTimer()
        {
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_buf.Length < MinScanLength) { _buf.Clear(); return; }
            var code = _buf.ToString().Trim();
            _buf.Clear();
            Fire(code);
        }

        private void Fire(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            System.Diagnostics.Debug.WriteLine($"[SCAN] fire '{code}'");
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                CodeScanned?.Invoke(code);
            }));
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}