using Cysharp.Text;

namespace SFE.Domain.Abstractions;

/// <summary>
/// Mutable side of <see cref="ITenantProvider"/>. Only the auth layer and the
/// bootstrap code should depend on this. Everyone else depends on the read-only
/// <see cref="ITenantProvider"/>.
/// </summary>
public interface ITenantContext : ITenantProvider
{
    void SignIn(
        int companyId, Ulid companySyncId,
        int userId, Ulid userSyncId,
        int? pointOfSaleId, Ulid? pointOfSaleSyncId);

    void SignOut();

    /// <summary>Enable bootstrap mode (seeding, first-run setup). No user context required.</summary>
    IDisposable EnterBootstrap();
}