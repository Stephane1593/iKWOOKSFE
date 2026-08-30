using SFE.Application.Interfaces;
using SFE.Domain.Enums;

namespace SFE.WPF.Services;

/// <summary>
/// Thin wrapper around <see cref="IManagerAuthorizationPrompter"/> +
/// <see cref="IManagerAuthorizationService"/>: shows the modal, consumes
/// the ticket, and returns a plain bool. All grant/deny audit rows are
/// written by ManagerAuthorizationService — no extra logging needed here.
/// </summary>
public sealed class ManagerGate
{
    private readonly IManagerAuthorizationPrompter _prompter;
    private readonly IManagerAuthorizationService _svc;

    public ManagerGate(IManagerAuthorizationPrompter prompter,
                       IManagerAuthorizationService svc)
    {
        _prompter = prompter;
        _svc = svc;
    }

    public async Task<bool> RequireAsync(ManagerAction action, AuthorizationContext ctx)
    {
        var ticket = await _prompter.RequireAsync(action, ctx);
        if (ticket is null) return false;
        return _svc.TryConsumeTicket(ticket.Value, action);
    }
}