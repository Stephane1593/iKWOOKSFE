using Cysharp.Text;

namespace SFE.Domain.Abstractions;

/// <summary>
/// Read-only current tenant/session context. Injected everywhere that needs to
/// know "who is doing this" — repositories, services, audit log, sync.
///
/// In the WPF app this is app-scoped: one user session per running instance.
/// </summary>
public interface ITenantProvider
{
    bool IsAuthenticated { get; }

    /// <summary>Throws if not authenticated. Use <see cref="IsAuthenticated"/> first.</summary>
    int CompanyId { get; }
    Ulid CompanySyncId { get; }

    int? CurrentUserId { get; }
    Ulid? CurrentUserSyncId { get; }

    int? CurrentPointOfSaleId { get; }
    Ulid? CurrentPointOfSaleSyncId { get; }

    /// <summary>
    /// True during database seeding, migrations, bootstrap screens where no user
    /// has logged in yet. Code paths that need this should be explicit.
    /// </summary>
    bool IsBootstrapMode { get; }
}