namespace SFE.WPF.Licensing;

/// <summary>
/// The Ed25519 public key used to verify license blobs. Regenerated per environment
/// (dev / staging / production). The pinned SHA-256 must match — if it doesn't,
/// the app refuses to start (see <see cref="Ed25519LicenseVerifier"/>).
/// </summary>
internal static class EmbeddedLicensePublicKey
{
    // 32-byte Ed25519 public key, hex.
    // Placeholder DEVELOPMENT key — REPLACE in production build.
    public const string PublicKeyHex =
        "90017a363efc44bf4c0c0a51315a2d4e26b8285c47b0fa3390b2b6ed9831f199";

    // SHA-256 of the raw 32 bytes above, upper-case hex. If they don't match,
    // Ed25519LicenseVerifier will throw at construction time — that's the point.
    public const string PublicKeySha256Hex =
        "115023BB1D2F7454AC53B4AB2F1BF17BC3980C88F5E9462B4F2075B691B156A8";

    public static byte[] GetBytes() => Convert.FromHexString(PublicKeyHex);
}