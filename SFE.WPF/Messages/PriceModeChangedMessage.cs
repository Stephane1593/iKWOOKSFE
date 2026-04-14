using CommunityToolkit.Mvvm.Messaging.Messages;
using SFE.Domain.Enums;

namespace SFE.WPF.Messages;

/// <summary>Broadcast quand le mode de prix change (HT ↔ TTC).</summary>
public class PriceModeChangedMessage : ValueChangedMessage<PriceMode>
{
    public PriceModeChangedMessage(PriceMode value) : base(value) { }
}

/// <summary>🆕 Broadcast quand le mode de remise change (avant/après taxe).</summary>
public class DiscountBeforeTaxChangedMessage : ValueChangedMessage<bool>
{
    public DiscountBeforeTaxChangedMessage(bool value) : base(value) { }
}