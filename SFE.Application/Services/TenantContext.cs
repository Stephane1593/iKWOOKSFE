using Cysharp.Text;
using SFE.Domain.Abstractions;

namespace SFE.Application.Services;

public sealed class TenantContext : ITenantContext
{
    private readonly object _gate = new();
    private int _bootstrapDepth;

    private int? _companyId;
    private Ulid? _companySyncId;
    private int? _userId;
    private Ulid? _userSyncId;
    private int? _posId;
    private Ulid? _posSyncId;

    public bool IsAuthenticated { get { lock (_gate) return _companyId.HasValue && _userId.HasValue; } }
    public bool IsBootstrapMode { get { lock (_gate) return _bootstrapDepth > 0; } }

    // Dans TenantContext
    public int CompanyId
    {
        get { lock (_gate) return _companyId ?? 0; }  // ⚠️ 0 = sentinel "aucun"
    }
    public Ulid CompanySyncId
    {
        get { lock (_gate) return _companySyncId ?? Ulid.Empty; }
    }

    public int? CurrentUserId { get { lock (_gate) return _userId; } }
    public Ulid? CurrentUserSyncId { get { lock (_gate) return _userSyncId; } }
    public int? CurrentPointOfSaleId { get { lock (_gate) return _posId; } }
    public Ulid? CurrentPointOfSaleSyncId { get { lock (_gate) return _posSyncId; } }

    public void SignIn(int companyId, Ulid companySyncId,
                       int userId, Ulid userSyncId,
                       int? pointOfSaleId, Ulid? pointOfSaleSyncId)
    {
        lock (_gate)
        {
            _companyId = companyId; _companySyncId = companySyncId;
            _userId = userId; _userSyncId = userSyncId;
            _posId = pointOfSaleId; _posSyncId = pointOfSaleSyncId;
        }
    }

    public void SignOut()
    {
        lock (_gate)
        {
            _companyId = null; _companySyncId = null;
            _userId = null; _userSyncId = null;
            _posId = null; _posSyncId = null;
        }
    }

    public IDisposable EnterBootstrap()
    {
        lock (_gate) _bootstrapDepth++;
        return new BootstrapScope(this);
    }

    private void ExitBootstrap()
    {
        lock (_gate) _bootstrapDepth = Math.Max(0, _bootstrapDepth - 1);
    }

    private sealed class BootstrapScope : IDisposable
    {
        private readonly TenantContext _ctx;
        private bool _disposed;
        public BootstrapScope(TenantContext ctx) => _ctx = ctx;
        public void Dispose() { if (!_disposed) { _ctx.ExitBootstrap(); _disposed = true; } }
    }
}