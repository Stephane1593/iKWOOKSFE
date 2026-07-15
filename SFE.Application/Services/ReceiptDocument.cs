namespace SFE.Application.Services;

public enum ReceiptAlign { Left, Center, Right }

public enum ReceiptElementType
{
    Text, Row, ThreeCol, DashLine, DoubleLine, Feed, QrCode, Logo
}


public class ReceiptElement
{
    public ReceiptElementType Type { get; set; }
    public string? Text { get; set; }   // Text / QrCode payload
    public string? Left { get; set; }   // Row / ThreeCol
    public string? Middle { get; set; } // ThreeCol
    public string? Right { get; set; }  // Row / ThreeCol
    public ReceiptAlign Align { get; set; } = ReceiptAlign.Left;
    public bool Bold { get; set; }
    public bool DoubleSize { get; set; }     // both dimensions
    public bool DoubleHeight { get; set; }   // height only
    public int FeedLines { get; set; } = 1;
}

public class ReceiptDocument
{
    public int Width { get; set; } = 48;
    public bool IsProforma { get; set; }
    public bool IsDuplicate { get; set; }
    public string FooterText { get; set; } = "Merci pour votre achat !";
    public string PrintedAt { get; set; } = "";
    public List<ReceiptElement> Elements { get; set; } = new();
}