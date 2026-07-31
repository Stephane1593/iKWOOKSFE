using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SFE.Application.Payments;

namespace SFE.Application.Services;

public sealed class OfflineQrService(byte[] pairingSecret, string caisseId)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        }
    };

    // Token may now be smaller due to gzip; adjust if needed
    public const int SafeTokenLength = 3000;

    public string Encode(OrderQrPayload payload)
    {
        // Serialize to JSON
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, Json);

        // Compress with gzip
        var compressed = Compress(json);

        // Sign the compressed data
        var body = B64Url(compressed);
        var sig = B64Url(Sign(body));

        return $"{body}.{sig}";
    }

    public bool TryDecode(string token, out OrderQrPayload? payload)
    {
        payload = null;

        var dot = token.IndexOf('.');
        if (dot <= 0) return false;

        var body = token[..dot];
        var sig = token[(dot + 1)..];

        byte[] providedSig;
        byte[] expectedSig;

        try
        {
            providedSig = FromB64Url(sig);
            expectedSig = Sign(body);
        }
        catch
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(providedSig, expectedSig))
            return false;

        try
        {
            var compressed = FromB64Url(body);
            var json = Decompress(compressed);
            payload = JsonSerializer.Deserialize<OrderQrPayload>(json, Json);
            return payload is not null;
        }
        catch
        {
            payload = null;
            return false;
        }
    }

    public OrderQrPayload BuildFor(
        string orderId,
        decimal amount,
        string currency,
        OfflineDocKind kind,
        ReceiptDocument[] documents,
        string? fiscalCode,
        string? fiscalQr)
    {
        var rawKey = $"OFF-{orderId}-{Guid.NewGuid():N}";
        var key = rawKey.Length > 40 ? rawKey[..40] : rawKey;

        return new OrderQrPayload(
            Version: 2,
            IdempotencyKey: key,
            OrderId: orderId,
            Amount: amount,
            Currency: currency,
            CaisseId: caisseId,
            IssuedUnix: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Kind: kind,
            Documents: documents,
            FiscalCode: kind == OfflineDocKind.Fiscal ? fiscalCode : null,
            FiscalQr: kind == OfflineDocKind.Fiscal ? fiscalQr : null);
    }

    private static byte[] Compress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            input.CopyTo(gz);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var gz = new GZipStream(input, CompressionMode.Decompress))
        {
            gz.CopyTo(output);
        }
        return output.ToArray();
    }

    private byte[] Sign(string body)
    {
        using var h = new HMACSHA256(pairingSecret);
        return h.ComputeHash(Encoding.ASCII.GetBytes(body));
    }

    private static string B64Url(byte[] b) =>
        Convert.ToBase64String(b)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] FromB64Url(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        t = (t.Length % 4) switch
        {
            2 => t + "==",
            3 => t + "=",
            0 => t,
            _ => throw new FormatException("Invalid base64url length.")
        };

        return Convert.FromBase64String(t);
    }
}