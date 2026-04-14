using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace SFE.WPF.Helpers;

public static class QrCodeHelper
{
    public static BitmapImage? Generate(string? content, int pixelsPerModule = 8)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            using var code = new PngByteQRCode(data);
            var bytes = code.GetGraphic(pixelsPerModule);

            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}