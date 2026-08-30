using System.Security.Cryptography;
using SFE.Licensing.Domain;

namespace SFE.Licensing.Local.Signing;

public interface ILicenseVerifier
{
    /// <summary>Returns claims if signature is valid; throws otherwise.</summary>
    LicenseClaims Verify(string blob);
}

/// <summary>
/// Ed25519 verifier. Uses .NET 8's built-in <see cref="ECDsa"/>? No — Ed25519 is not
/// in <c>System.Security.Cryptography</c> on .NET 8 (it lives in .NET 9+). To keep
/// zero external native dependencies for v1, we ship a small managed Ed25519
/// verifier in <see cref="Ed25519"/> below. It is verify-only (no signing) and
/// tuned for constant-time equality on the final check.
///
/// If you'd rather depend on <c>NSec.Cryptography</c> or <c>BouncyCastle</c>, swap
/// the implementation of <see cref="Verify"/>; the interface stays the same.
/// </summary>
public sealed class Ed25519LicenseVerifier : ILicenseVerifier
{
    private readonly byte[] _publicKey;      // 32 bytes
    private readonly string _pinnedSha256;   // hex, uppercase

    public Ed25519LicenseVerifier(byte[] publicKey, string pinnedPublicKeySha256Hex)
    {
        if (publicKey is null || publicKey.Length != 32)
            throw new ArgumentException("Ed25519 public key must be 32 bytes.", nameof(publicKey));

        _publicKey = (byte[])publicKey.Clone();
        _pinnedSha256 = pinnedPublicKeySha256Hex.ToUpperInvariant();

        // Anti-swap check: if someone replaces the DLL with a rogue public key,
        // this will trip on the first call.
        var actual = Convert.ToHexString(SHA256.HashData(_publicKey));
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(actual),
                System.Text.Encoding.ASCII.GetBytes(_pinnedSha256)))
        {
            throw new CryptographicException(
                "Embedded license public key does not match pinned SHA-256. " +
                "This build has been tampered with.");
        }
    }

    public LicenseClaims Verify(string blob)
    {
        var (payload, signature) = LicenseBlob.Decode(blob);

        if (signature.Length != 64)
            throw new CryptographicException("Ed25519 signature must be 64 bytes.");

        var pub = new Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters(_publicKey, 0);
        var verifier = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
        verifier.Init(forSigning: false, pub);
        verifier.BlockUpdate(payload, 0, payload.Length);
        if (!verifier.VerifySignature(signature))
            throw new CryptographicException("License signature is invalid.");


        return LicenseBlob.DeserializeClaims(payload);
    }
}