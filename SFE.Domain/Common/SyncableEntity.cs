namespace SFE.Domain.Common;

/// <summary>
/// Base for every tenant-scoped entity. Adds CompanyId to the root contract.
/// 99% of entities inherit this. Only <c>Company</c> itself uses <see cref="SyncableRootEntity"/> directly.
/// </summary>
public abstract class SyncableEntity : SyncableRootEntity
{
    public int CompanyId { get; set; }
}