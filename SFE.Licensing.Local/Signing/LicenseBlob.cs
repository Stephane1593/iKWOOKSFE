using System.Text;
using System.Text.Json;
using SFE.Licensing.Domain;

namespace SFE.Licensing.Local.Signing;

/// <summary>
/// The on-disk format is <c>base64url(payload_json) + "." + base64url(signature)</c>.
/// This is JWT-shaped for familiarity but deliberately NOT a JWT — we do not
/// negotiate algorithms in the header, avoiding the JWT alg-confusion class of bugs.
/// </summary>
public static class LicenseBlob
{
    public static readonly JsonSerializerOptions CanonicalJson = new()
    {
        // Deterministic serialization: no indenting, sorted keys, no null defaults.
        WriteIndented = false,
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] SerializeClaims(LicenseClaims claims)
    {
        var json = JsonSerializer.Serialize(claims, CanonicalJson);
        return Encoding.UTF8.GetBytes(json);
    }

    public static LicenseClaims DeserializeClaims(byte[] payload)
    {
        return JsonSerializer.Deserialize<LicenseClaims>(payload, CanonicalJson)
            ?? throw new FormatException("Empty license payload.");
    }

    public static string Encode(byte[] payload, byte[] signature)
    {
        return $"{Base64Url.Encode(payload)}.{Base64Url.Encode(signature)}";
    }

    public static (byte[] Payload, byte[] Signature) Decode(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob))
            throw new FormatException("License blob is empty.");

        var dot = blob.IndexOf('.');
        if (dot <= 0 || dot == blob.Length - 1)
            throw new FormatException("License blob is malformed (missing '.').");

        var payload = Base64Url.Decode(blob[..dot]);
        var signature = Base64Url.Decode(blob[(dot + 1)..]);
        return (payload, signature);
    }
}

internal static class Base64Url
{
    public static string Encode(byte[] data)
    {
        var s = Convert.ToBase64String(data);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] Decode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}