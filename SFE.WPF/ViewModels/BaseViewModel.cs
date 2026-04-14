using CommunityToolkit.Mvvm.ComponentModel;
using SFE.Application.Events;

namespace SFE.WPF.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pageTitle = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    // ── Status helpers ──

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _showSuccess;

    [ObservableProperty]
    private bool _showError;

    private readonly List<Func<AppEventArgs, Task>> _eventSubscriptions = new();
    private bool _disposed;

    protected async Task ShowSuccessAsync(string message, int delayMs = 3000)
    {
        StatusMessage = message;
        ShowSuccess = true;
        ShowError = false;
        await Task.Delay(delayMs);
        ShowSuccess = false;
    }

    protected void ShowErrorMessage(string message)
    {
        StatusMessage = message;
        ShowError = true;
        ShowSuccess = false;
    }

    protected void ClearStatus()
    {
        ShowSuccess = false;
        ShowError = false;
        StatusMessage = "";
    }

    /// <summary>
    /// Subscribe to one or more AppEvents. The handler runs when any of the
    /// listed events is published. Subscriptions are automatically cleaned up
    /// on Dispose().
    /// </summary>
    protected void Subscribe(Func<Task> handler, params AppEvent[] eventTypes)
    {
        var set = new HashSet<AppEvent>(eventTypes);
        Func<AppEventArgs, Task> wrapper = async (args) =>
        {
            if (_disposed) return;
            if (set.Contains(args.Event))
            {
                try { await handler(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[{GetType().Name}] Event handler error: {ex.Message}");
                }
            }
        };

        _eventSubscriptions.Add(wrapper);
        AppEventBus.Subscribe(wrapper);
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sub in _eventSubscriptions)
            AppEventBus.Unsubscribe(sub);
        _eventSubscriptions.Clear();
    }
}