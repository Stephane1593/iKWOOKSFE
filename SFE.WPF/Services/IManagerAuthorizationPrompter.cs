using SFE.Application.Interfaces;
using SFE.Domain.Enums;

namespace SFE.WPF.Services;

public interface IManagerAuthorizationPrompter
{
    /// <summary>
    /// Shows the modal, awaits scan/PIN/credentials, returns a valid ticket
    /// or null if user cancelled / verification failed.
    /// </summary>
    Task<Guid?> RequireAsync(ManagerAction action, AuthorizationContext ctx);
}