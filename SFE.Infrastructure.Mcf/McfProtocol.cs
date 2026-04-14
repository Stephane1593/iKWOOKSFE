using System.Text;

namespace SFE.Infrastructure.Mcf;

/// <summary>
/// Implémente le protocole binaire MCF-SFE (spec pages 5-6).
/// Trame: SOH(01h) LEN SEQ CMD DATA AMB(05h) BCC(4 octets) ETX(03h)
/// </summary>
public static class McfProtocol
{
    public const byte SOH = 0x01;
    public const byte ETX = 0x03;
    public const byte BRK = 0x04;
    public const byte AMB = 0x05;
    public const byte NAK = 0x15;
    public const byte SYN = 0x16;
    public const byte OFFSET = 0x20;
    public const byte BCC_OFFSET = 0x30;

    // ── Codes de commande ──
    public const byte CMD_STATUS = 0xC1;
    public const byte CMD_SERVER_STATUS = 0xC2;
    public const byte CMD_TAXPAYER_INFO = 0x2B;
    public const byte CMD_CLIENT_INFO = 0xC3;
    public const byte CMD_NEW_INVOICE = 0xC0;
    public const byte CMD_REGISTER_ITEM = 0x31;
    public const byte CMD_SUBTOTAL = 0x33;
    public const byte CMD_PAYMENT = 0x35;
    public const byte CMD_ADDITIONAL_INFO = 0x36;
    public const byte CMD_FINALIZE = 0x38;

    /// <summary>
    /// Construit une trame de commande MCF complète.
    /// </summary>
    public static byte[] BuildCommand(byte seq, byte cmd, string? data = null)
    {
        byte[] dataBytes = data != null ? Encoding.UTF8.GetBytes(data) : Array.Empty<byte>();

        // LEN = nombre d'octets de LEN à AMB inclus + offset 0x20
        // LEN(1) + SEQ(1) + CMD(1) + DATA(n) + AMB(1) = 4 + dataBytes.Length
        int bodyLen = 4 + dataBytes.Length;
        byte lenByte = (byte)(bodyLen + OFFSET);

        // Construire le corps (LEN à AMB) pour le calcul BCC
        var body = new List<byte>();
        body.Add(lenByte);
        body.Add((byte)(seq + OFFSET));
        body.Add(cmd);
        body.AddRange(dataBytes);
        body.Add(AMB);

        // Calcul BCC : somme de tous les octets du corps
        ushort bccSum = 0;
        foreach (byte b in body)
            bccSum += b;

        byte[] bcc = EncodeBcc(bccSum);

        // Assembler la trame complète
        var frame = new List<byte>();
        frame.Add(SOH);
        frame.AddRange(body);
        frame.AddRange(bcc);
        frame.Add(ETX);

        return frame.ToArray();
    }

    /// <summary>
    /// Parse une réponse MCF complète.
    /// Trame: SOH LEN SEQ CMD DATA BRK STA(6) AMB BCC(4) ETX
    /// </summary>
    public static McfResponse? ParseResponse(byte[] buffer, int length)
    {
        if (length < 1) return null;

        // Réponse mono-octet ?
        if (length == 1)
        {
            if (buffer[0] == NAK) return new McfResponse { IsNak = true };
            if (buffer[0] == SYN) return new McfResponse { IsSyn = true };
        }

        // Chercher SOH
        int start = -1;
        for (int i = 0; i < length; i++)
        {
            if (buffer[i] == SOH) { start = i; break; }
        }
        if (start < 0) return null;

        // Chercher ETX
        int end = -1;
        for (int i = start + 1; i < length; i++)
        {
            if (buffer[i] == ETX) { end = i; break; }
        }
        if (end < 0) return null;

        int frameLen = end - start + 1;
        if (frameLen < 12) return null; // SOH + LEN + SEQ + CMD + BRK + STA(6) + AMB + BCC(4) + ETX = 16 min

        byte lenByte = buffer[start + 1];
        byte seqByte = buffer[start + 2];
        byte cmdByte = buffer[start + 3];

        int declaredLen = lenByte - OFFSET; // Nombre d'octets de LEN à AMB

        // Trouver AMB (0x05) en partant de la fin avant BCC
        // BCC = 4 octets avant ETX
        // AMB = 1 octet avant BCC
        int ambPos = end - 5; // ETX(-1) - BCC(4) - AMB = end - 5
        if (ambPos <= start) return null;

        // STA = 6 octets avant AMB
        int staStart = ambPos - 6;
        if (staStart <= start) return null;

        // BRK = 1 octet avant STA
        int brkPos = staStart - 1;
        if (brkPos <= start + 3) return null; // Doit être après CMD au minimum

        // DATA = entre CMD et BRK
        int dataStart = start + 4;
        int dataLen = brkPos - dataStart;
        string data = "";
        if (dataLen > 0)
        {
            data = Encoding.UTF8.GetString(buffer, dataStart, dataLen);
        }

        // STA bytes
        byte[] sta = new byte[6];
        Array.Copy(buffer, staStart, sta, 0, 6);

        // Vérifier BCC
        int bccStart = ambPos + 1;
        // Corps = de LEN à AMB
        ushort expectedBcc = 0;
        for (int i = start + 1; i <= ambPos; i++)
            expectedBcc += buffer[i];

        ushort actualBcc = DecodeBcc(buffer, bccStart);

        return new McfResponse
        {
            Seq = (byte)(seqByte - OFFSET),
            Cmd = cmdByte,
            Data = data,
            StatusBytes = sta,
            IsValid = expectedBcc == actualBcc,
            BccMatch = expectedBcc == actualBcc
        };
    }

    /// <summary>
    /// Encode BCC en 4 octets ASCII avec offset 0x30.
    /// Ex: 0x12AB → {0x31, 0x32, 0x3A, 0x3B}
    /// </summary>
    private static byte[] EncodeBcc(ushort sum)
    {
        byte[] bcc = new byte[4];
        bcc[0] = (byte)(((sum >> 12) & 0x0F) + BCC_OFFSET);
        bcc[1] = (byte)(((sum >> 8) & 0x0F) + BCC_OFFSET);
        bcc[2] = (byte)(((sum >> 4) & 0x0F) + BCC_OFFSET);
        bcc[3] = (byte)((sum & 0x0F) + BCC_OFFSET);
        return bcc;
    }

    private static ushort DecodeBcc(byte[] buffer, int offset)
    {
        int b0 = (buffer[offset] - BCC_OFFSET) & 0x0F;
        int b1 = (buffer[offset + 1] - BCC_OFFSET) & 0x0F;
        int b2 = (buffer[offset + 2] - BCC_OFFSET) & 0x0F;
        int b3 = (buffer[offset + 3] - BCC_OFFSET) & 0x0F;
        return (ushort)((b0 << 12) | (b1 << 8) | (b2 << 4) | b3);
    }

    /// <summary>
    /// Échappe les caractères spéciaux dans les données (spec 4.2)
    /// </summary>
    public static string EscapeData(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("&", "^amp;")
            .Replace(",", "^x2c;")
            .Replace("<", "^lt;")
            .Replace(">", "^gt;")
            .Replace("\r", "^xa;")
            .Replace("\n", "^xd;");
    }
}

public class McfResponse
{
    public bool IsNak { get; set; }
    public bool IsSyn { get; set; }
    public byte Seq { get; set; }
    public byte Cmd { get; set; }
    public string Data { get; set; } = "";
    public byte[] StatusBytes { get; set; } = new byte[6];
    public bool IsValid { get; set; }
    public bool BccMatch { get; set; }

    public bool IsError => !IsValid || IsNak;
    public string[] Fields => Data.Split(',');
}