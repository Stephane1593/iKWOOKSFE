namespace SFE.Licensing.Domain;

/// <summary>
/// Value type: a 64-char hex string (SHA-256). Two equal fingerprints mean
/// "same machine, same install" from the licensing system's point of view.
/// </summary>
public readonly record struct MachineFingerprint(string Value)
{
    public static MachineFingerprint Empty => new(string.Empty);
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value;
}