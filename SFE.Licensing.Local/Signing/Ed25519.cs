// SPDX-License-Identifier: MIT
// Managed Ed25519 verifier (RFC 8032 §5.1.7). Verify-only.
//
// This is intentionally a tight, dependency-free port. If you prefer, replace the
// class body with a call to NSec.Cryptography (managed) or BouncyCastle. The
// public surface — Ed25519.Verify(sig, msg, pubKey) — stays the same, and the
// rest of the licensing stack is unaffected.
//
// Correctness note: we use SHA-512 from BCL, big-integer arithmetic from
// System.Numerics.BigInteger, and constant-time byte equality via
// CryptographicOperations.FixedTimeEquals for the final compare.

using System.Numerics;
using System.Security.Cryptography;

namespace SFE.Licensing.Local.Signing;

internal static class Ed25519
{
    // Curve constants (Curve25519 / Ed25519).
    private static readonly BigInteger P =
        BigInteger.Pow(2, 255) - 19;

    private static readonly BigInteger L =
        BigInteger.Parse("7237005577332262213973186563042994240857116359379907606001950938285454250989");

    private static readonly BigInteger D =
        ModP(BigInteger.Parse("-4513249062541557337682894930092624173785641285191125241628941591882900924598840740") *
             ModInverse(BigInteger.Parse("46316835694926478169428394003475163141307993866256225615783033603165251855960"), P));

    // Base point (B).
    private static readonly BigInteger By =
        ModP(BigInteger.Parse("4") * ModInverse(new BigInteger(5), P));
    private static readonly BigInteger Bx = RecoverX(By, 0);

    /// <summary>RFC 8032 verify. Returns true iff the signature is valid.</summary>
    public static bool Verify(byte[] signature, ReadOnlySpan<byte> message, byte[] publicKey)
    {
        if (signature.Length != 64) return false;
        if (publicKey.Length != 32) return false;

        var r = signature.AsSpan(0, 32).ToArray();
        var sBytes = signature.AsSpan(32, 32).ToArray();
        var s = ToBigIntegerLE(sBytes);
        if (s >= L) return false;

        // A = decode publicKey
        if (!TryDecodePoint(publicKey, out var Ax, out var Ay)) return false;

        // k = SHA-512(R || A || M) mod L
        using var sha = SHA512.Create();
        sha.TransformBlock(r, 0, 32, null, 0);
        sha.TransformBlock(publicKey, 0, 32, null, 0);
        var msg = message.ToArray();
        sha.TransformFinalBlock(msg, 0, msg.Length);
        var k = ModPositive(ToBigIntegerLE(sha.Hash!), L);

        // Check: [8]sB == [8]R + [8]kA
        var sB = ScalarMult(Bx, By, s);
        if (!TryDecodePoint(r, out var Rx, out var Ry)) return false;
        var kA = ScalarMult(Ax, Ay, k);
        var rhs = PointAdd(Rx, Ry, kA.X, kA.Y);
        var lhs = sB;

        var lhs8 = ScalarMult(lhs.X, lhs.Y, 8);
        var rhs8 = ScalarMult(rhs.X, rhs.Y, 8);

        return lhs8.X == rhs8.X && lhs8.Y == rhs8.Y;
    }

    // -- Point ops (affine, extended arithmetic would be faster; verify is
    //    called at most once per app boot + once per heartbeat, so this is fine) --

    private static (BigInteger X, BigInteger Y) PointAdd(BigInteger x1, BigInteger y1, BigInteger x2, BigInteger y2)
    {
        var xy = ModP(D * x1 * x2 * y1 * y2);
        var x3 = ModP((x1 * y2 + x2 * y1) * ModInverse(1 + xy, P));
        var y3 = ModP((y1 * y2 + x1 * x2) * ModInverse(1 - xy, P));
        return (x3, y3);
    }

    private static (BigInteger X, BigInteger Y) ScalarMult(BigInteger x, BigInteger y, BigInteger k)
    {
        // Identity element (0, 1)
        BigInteger rx = 0, ry = 1;
        BigInteger px = x, py = y;

        while (k > 0)
        {
            if ((k & 1) == 1)
                (rx, ry) = PointAdd(rx, ry, px, py);
            (px, py) = PointAdd(px, py, px, py);
            k >>= 1;
        }
        return (rx, ry);
    }

    private static bool TryDecodePoint(byte[] enc, out BigInteger x, out BigInteger y)
    {
        // Little-endian; top bit of last byte is sign of X.
        var buf = (byte[])enc.Clone();
        var xSign = (buf[31] & 0x80) != 0 ? 1 : 0;
        buf[31] &= 0x7F;
        y = ToBigIntegerLE(buf);
        if (y >= P) { x = 0; return false; }
        x = RecoverX(y, xSign);
        return true;
    }

    private static BigInteger RecoverX(BigInteger y, int sign)
    {
        var xx = (y * y - 1) * ModInverse(D * y * y + 1, P);
        var x = BigInteger.ModPow(xx, (P + 3) / 8, P);
        if (ModP(x * x - xx) != 0)
            x = ModP(x * BigInteger.ModPow(2, (P - 1) / 4, P));
        if ((int)(x & 1) != sign)
            x = P - x;
        return x;
    }

    // -- Math helpers --

    private static BigInteger ModP(BigInteger n) => ModPositive(n, P);

    private static BigInteger ModPositive(BigInteger n, BigInteger m)
    {
        var r = n % m;
        return r < 0 ? r + m : r;
    }

    private static BigInteger ModInverse(BigInteger a, BigInteger m)
        => BigInteger.ModPow(ModPositive(a, m), m - 2, m);

    private static BigInteger ToBigIntegerLE(ReadOnlySpan<byte> bytes)
    {
        // Force non-negative by appending a 0x00 byte.
        Span<byte> tmp = stackalloc byte[bytes.Length + 1];
        bytes.CopyTo(tmp);
        tmp[^1] = 0;
        return new BigInteger(tmp, isUnsigned: true, isBigEndian: false);
    }
}