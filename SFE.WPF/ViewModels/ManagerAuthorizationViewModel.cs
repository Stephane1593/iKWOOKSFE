using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;
using SFE.Domain.Enums;

namespace SFE.WPF.ViewModels;

public partial class ManagerAuthorizationViewModel : ObservableObject
{
    private readonly IManagerAuthorizationService _svc;
    private readonly AuthorizationContext _ctx;

    public ManagerAction Action { get; }
    public string ActionLabel => Action.Label();
    public Guid TicketId { get; private set; }

    [ObservableProperty] private int _selectedTabIndex; // 0=barcode, 1=pin, 2=credentials

    // Barcode
    [ObservableProperty] private string _barcodePayload = "";
    // PIN
    [ObservableProperty] private string _pinUsername = "";
    [ObservableProperty] private string _pin = "";
    // Credentials
    [ObservableProperty] private string _credUsername = "";
    [ObservableProperty] private string _credPassword = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;

    public event Action? Succeeded;
    public event Action? Cancelled;

    public ManagerAuthorizationViewModel(
        IManagerAuthorizationService svc,
        ManagerAction action,
        AuthorizationContext ctx)
    {
        _svc = svc; Action = action; _ctx = ctx;
    }

    /// <summary>
    /// Called by the dialog code-behind on Enter/scan-terminator when
    /// the Barcode textbox has focus. Barcodes end with CR/LF from HID
    /// scanners so we treat "text change with newline" as "submit".
    /// </summary>
    [RelayCommand]
    private async Task SubmitBarcodeAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(BarcodePayload)) return;
        await RunAsync(() => _svc.VerifyBarcodeAsync(BarcodePayload.Trim(), Action, _ctx));
    }

    [RelayCommand]
    private async Task SubmitPinAsync()
    {
        if (IsBusy) return;
        await RunAsync(() => _svc.VerifyPinAsync(
            string.IsNullOrWhiteSpace(PinUsername) ? null : PinUsername.Trim(),
            Pin, Action, _ctx));
    }

    [RelayCommand]
    private async Task SubmitCredentialsAsync()
    {
        if (IsBusy) return;
        await RunAsync(() => _svc.VerifyCredentialsAsync(
            CredUsername.Trim(), CredPassword, Action, _ctx));
    }

    [RelayCommand] private void Cancel() => Cancelled?.Invoke();

    private async Task RunAsync(Func<Task<AuthorizationResult>> op)
    {
        HasError = false; ErrorMessage = ""; IsBusy = true;
        try
        {
            var r = await op();
            if (r.Granted)
            {
                TicketId = r.TicketId;
                Succeeded?.Invoke();
            }
            else
            {
                ErrorMessage = r.ErrorMessage ?? "Refusé.";
                HasError = true;
                // Clear sensitive fields on failure
                Pin = ""; CredPassword = ""; BarcodePayload = "";
            }
        }
        finally { IsBusy = false; }
    }
}