using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SFE.Application.Interfaces;

namespace SFE.WPF.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isLoading;

    /// <summary>Raised when authentication succeeds — LoginWindow should close.</summary>
    public event Action? LoginSucceeded;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Called from code-behind (PasswordBox doesn't support binding).
    /// </summary>
    public async Task LoginAsync(string password)
    {
        // ── Validation ──
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Veuillez saisir votre nom d'utilisateur.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Veuillez saisir votre mot de passe.";
            HasError = true;
            return;
        }

        // ── Authenticate ──
        IsLoading = true;
        try
        {
            var user = await _authService.LoginAsync(Username.Trim(), password);

            if (user == null)
            {
                ErrorMessage = "Identifiants incorrects. Vérifiez et réessayez.";
                HasError = true;
                return;
            }

            if (!user.IsActive)
            {
                ErrorMessage = "Ce compte a été désactivé. Contactez l'administrateur.";
                HasError = true;
                _authService.Logout();
                return;
            }

            // ✅ Success
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de connexion : {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}