using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using SFE.Licensing.Domain;

class Program
{
    static string B64Url(byte[] d) =>
        Convert.ToBase64String(d).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "genkeys")
        {
            GenerateKeys();
            return;
        }

        MintForCustomer();
    }

    // -----------------------------------------------------------
    //  RUN THIS ONCE to create your secret + public keys
    //  Command:  dotnet run genkeys
    // -----------------------------------------------------------
    static void GenerateKeys()
    {
        var g = new Ed25519KeyPairGenerator();
        g.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var kp = g.GenerateKeyPair();

        var pub = ((Ed25519PublicKeyParameters)kp.Public).GetEncoded();
        var priv = ((Ed25519PrivateKeyParameters)kp.Private).GetEncoded();

        Console.WriteLine("=================================================");
        Console.WriteLine("COPY THESE TWO INTO EmbeddedLicensePublicKey.cs:");
        Console.WriteLine("=================================================");
        Console.WriteLine("PublicKeyHex       = " + Convert.ToHexString(pub).ToLower());
        Console.WriteLine("PublicKeySha256Hex = " +
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pub)));
        Console.WriteLine();
        Console.WriteLine("=================================================");
        Console.WriteLine("SAVE THIS SOMEWHERE SECRET (never ships in app):");
        Console.WriteLine("=================================================");
        Console.WriteLine("PrivateKeyHex      = " + Convert.ToHexString(priv));
        Console.WriteLine();
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
    }

    // -----------------------------------------------------------
    //  RUN THIS to make a license for one customer
    //  Command:  dotnet run
    // -----------------------------------------------------------
    static void MintForCustomer()
    {
        // 1) Paste your PRIVATE key here (from genkeys):
        const string privateKeyHex = "BAA3069770445DFE9D57D0E338EDBC4B6CEE5C436D533C09A233D9A9E677CC6C";

        // 2) Ask for the customer details when you run it:
        Console.Write("Company name        : ");
        var company = Console.ReadLine() ?? "";

        Console.Write("Machine fingerprint : ");
        var fingerprint = (Console.ReadLine() ?? "").Trim();

        Console.Write("Years valid (e.g. 1): ");
        int years = int.TryParse(Console.ReadLine(), out var y) ? y : 1;

        var claims = new LicenseClaims
        {
            LicenseId = Guid.NewGuid().ToString("N"),
            CompanyName = company,
            Edition = "Standalone",
            MaxPointsOfSale = 1,
            MaxUsers = 5,
            ActivationSlots = 1,
            Features = new()
            {
                "bulk_invoicing",
                "loyalty",
                "stock_transfers",
                "multi_pos",
                "advanced_reports"
            },
            IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            NotBeforeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddYears(years).ToUnixTimeSeconds(),
            GraceDays = 14,
            HeartbeatIntervalHours = 6,
            Kind = "full",
            BoundFingerprint = fingerprint
        };

        var blob = Mint(Convert.FromHexString(privateKeyHex), claims);

        var fileName = $"{company.Replace(" ", "_")}.lic";
        File.WriteAllText(fileName, blob);

        Console.WriteLine();
        Console.WriteLine($"DONE. Created file: {fileName}");
        Console.WriteLine("Send that file to the customer.");
        Console.WriteLine();
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
    }

    static string Mint(byte[] privKey, LicenseClaims claims)
    {
        var json = JsonSerializer.Serialize(claims, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var payload = Encoding.UTF8.GetBytes(json);

        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(privKey, 0));
        signer.BlockUpdate(payload, 0, payload.Length);
        var sig = signer.GenerateSignature();

        return $"{B64Url(payload)}.{B64Url(sig)}";
    }
}