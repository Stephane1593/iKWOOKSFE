using SFE.WPF.Models;

namespace SFE.WPF.Services;

/// <summary>
/// Singleton that holds the current cash session state.
/// Set after the session-open dialog, cleared on logout / Z-close.
/// Supports IT Tech "setup mode" where no cash session is active
/// but the operator can access configuration screens.
/// </summary>
public class CashSessionState
{
    public CashSessionInfo? Current { get; private set; }

    /// <summary>
    /// True when a normal cash session is active (has POS, amounts, etc.).
    /// </summary>
    public bool IsSessionOpen => Current != null;

    /// <summary>
    /// True when an IT Tech bypassed the session-open dialog
    /// to access configuration only (no POS, no invoicing).
    /// </summary>
    public bool IsSetupMode { get; private set; }

    /// <summary>
    /// The operator name in setup mode (since there's no CashSessionInfo).
    /// </summary>
    public string? SetupModeOperator { get; private set; }

    /// <summary>
    /// True if the app is in any active state (normal session or setup mode).
    /// </summary>
    public bool IsActive => IsSessionOpen || IsSetupMode;

    /// <summary>
    /// Opens a normal cash session with full POS and opening amounts.
    /// </summary>
    public void Open(CashSessionInfo session)
    {
        Current = session ?? throw new ArgumentNullException(nameof(session));
        IsSetupMode = false;
        SetupModeOperator = null;
    }

    /// <summary>
    /// IT Tech bypass — enters setup mode.
    /// If a session is already open, it is preserved (not cleared).
    /// </summary>
    public void EnterSetupMode(string operatorName)
    {
        // ★ Don't clear Current — the session might still be active
        // from a previous user. IT Tech just layers on top.
        IsSetupMode = true;
        SetupModeOperator = operatorName;
    }

    /// <summary>
    /// Exits setup mode without affecting the underlying session.
    /// Called when IT Tech logs out but a session was still active underneath.
    /// </summary>
    public void ExitSetupMode()
    {
        IsSetupMode = false;
        SetupModeOperator = null;
    }

    /// <summary>
    /// Clears everything — called on logout or Z-close.
    /// </summary>
    public void Close()
    {
        Current = null;
        IsSetupMode = false;
        SetupModeOperator = null;
    }
}