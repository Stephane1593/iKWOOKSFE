// SFE.WPF/ViewModels/IActivatable.cs
namespace SFE.WPF.ViewModels;

/// <summary>
/// Implemented by ViewModels that need to refresh data 
/// when their page becomes visible again (cached pages).
/// </summary>
public interface IActivatable
{
    Task ActivateAsync();
}