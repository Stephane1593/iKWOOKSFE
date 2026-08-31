// SFE.Domain/Enums/PaymentChannel.cs
namespace SFE.Domain.Enums;

public enum PaymentChannel
{
    LocalPos = 0,      // encaissé à la caisse
    SunmiTerminal = 1  // délégué à un Sunmi portable
}

public enum SunmiHandoff
{
    None = 0,
    ShowQr = 1,        // afficher un QR que le Sunmi scanne
    LanDevice = 2      // pousser vers un Sunmi découvert sur le LAN
}