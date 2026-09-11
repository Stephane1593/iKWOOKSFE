using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SFE.WPF.Messages;

/// <summary>
/// Message envoyé lorsqu'une table est sélectionnée pour ouvrir la caisse.
/// </summary>
public class OpenTableMessage : ValueChangedMessage<(int TableId, int? OrderId)>
{
    public OpenTableMessage(int tableId, int? orderId = null)
        : base((tableId, orderId))
    {
    }
}