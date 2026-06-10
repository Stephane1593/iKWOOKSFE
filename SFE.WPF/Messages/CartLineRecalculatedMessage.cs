using CommunityToolkit.Mvvm.Messaging.Messages;
using SFE.WPF.ViewModels;

namespace SFE.WPF.Messages;

/// <summary>
/// Émis par une ligne du panier après qu'elle s'est auto-recalculée
/// (édition directe de la quantité via TextBox, etc.).
/// Permet à <see cref="PosViewModel"/> de rafraîchir les totaux globaux
/// sans que la ligne ait besoin d'une référence au VM parent.
/// </summary>
public sealed class CartLineRecalculatedMessage : ValueChangedMessage<CartItemViewModel>
{
    public CartLineRecalculatedMessage(CartItemViewModel item) : base(item) { }
}