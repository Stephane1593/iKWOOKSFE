using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SFE.Application.Payments;

namespace SFE.Application.Services;

public sealed class OfflineQrService(byte[] pairingSecret, string caisseId)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A version-40 QR at ECC level M holds ~2300 bytes of alphanumeric.
    // Our base64url token is ASCII, so this is the practical ceiling before
    // the code becomes too dense to scan reliably on a phone-grade camera.
    private const int SafeTokenLength = 1200;

    public string Encode(OrderQrPayload payload)
    {
        var body = B64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Json));
        var sig = B64Url(Sign(body));
        var token = $"{body}.{sig}";

        // If embedding the fiscal QR blew the budget, fall back to a lean token:
        // the Sunmi will fetch the fiscal doc on reconnect instead of printing
        // it offline. Payment still works; only the OFFLINE fiscal print degrades.
        if (token.Length > SafeTokenLength && payload.FiscalQr is not null)
            return Encode(payload with
            {
                Kind = OfflineDocKind.Provisional,
                FiscalQr = null   // drop the heavy field, keep the code
            });

        return token;
    }

    public bool TryDecode(string token, out OrderQrPayload? payload)
    {
        payload = null;
        var dot = token.IndexOf('.');
        if (dot <= 0) return false;

        var body = token[..dot];
        var sig = token[(dot + 1)..];

        if (!CryptographicOperations.FixedTimeEquals(FromB64Url(sig), Sign(body)))
            return false;

        payload = JsonSerializer.Deserialize<OrderQrPayload>(FromB64Url(body), Json);
        return payload is not null;
    }

    public OrderQrPayload BuildFor(
        string orderId, decimal amount, string currency,
        OfflineDocKind kind, string? fiscalCode, string? fiscalQr) => new(
            Version: 1,
            IdempotencyKey: $"OFF-{orderId}-{Guid.NewGuid():N}"[..40],
            OrderId: orderId,
            Amount: amount,
            Currency: currency,
            CaisseId: caisseId,
            IssuedUnix: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Kind: kind,
            FiscalCode: kind == OfflineDocKind.Fiscal ? fiscalCode : null,
            FiscalQr: kind == OfflineDocKind.Fiscal ? fiscalQr : null);

    private byte[] Sign(string body)
    {
        using var h = new HMACSHA256(pairingSecret);
        return h.ComputeHash(Encoding.ASCII.GetBytes(body));
    }

    private static string B64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromB64Url(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        t = (t.Length % 4) switch { 2 => t + "==", 3 => t + "=", _ => t };
        return Convert.FromBase64String(t);
    }
}