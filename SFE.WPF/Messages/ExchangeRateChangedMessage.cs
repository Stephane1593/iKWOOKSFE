using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SFE.WPF.Messages;

/// <summary>
/// Broadcast when exchange rates change so POS/Invoice screens
/// pick up the new values without restarting.
/// </summary>
public class ExchangeRateChangedMessage : ValueChangedMessage<ExchangeRatePayload>
{
    public ExchangeRateChangedMessage(ExchangeRatePayload value) : base(value) { }
}

public record ExchangeRatePayload(
    decimal UsdRate,
    decimal EurRate,
    decimal CnyRate,
    DateTime? DgiDate);