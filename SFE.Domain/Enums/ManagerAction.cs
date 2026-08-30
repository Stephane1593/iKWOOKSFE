namespace SFE.Domain.Enums;

/// <summary>
/// Every action that requires manager authorization at the till or elsewhere.
/// Each value maps 1:1 to a permission key on Role.Permissions
/// (e.g. ManagerAction.RemoveCartLine -> "authorize.removeCartLine").
/// </summary>
public enum ManagerAction
{
    RemoveCartLine = 1,
    ClearCart = 2,
    ApplyLargeDiscount = 3,   // > seuil paramétrable
    OverridePrice = 4,
    CancelInvoice = 5,
    IssueCreditNote = 6,
    ReopenSession = 7,
    NoSaleDrawer = 8,
    NegativeStockSale = 9,
    DeleteProduct = 10,
    ChangeExchangeRate = 11,
    ReprintFiscalReceipt = 12,
}

public static class ManagerActionExtensions
{
    /// <summary>
    /// Permission key on Role.Permissions JSON. A role that carries this key
    /// with value=true is allowed to authorize the action.
    /// </summary>
    public static string PermissionKey(this ManagerAction a) => a switch
    {
        ManagerAction.RemoveCartLine => "authorize.removeCartLine",
        ManagerAction.ClearCart => "authorize.clearCart",
        ManagerAction.ApplyLargeDiscount => "authorize.largeDiscount",
        ManagerAction.OverridePrice => "authorize.overridePrice",
        ManagerAction.CancelInvoice => "authorize.cancelInvoice",
        ManagerAction.IssueCreditNote => "authorize.issueCreditNote",
        ManagerAction.ReopenSession => "authorize.reopenSession",
        ManagerAction.NoSaleDrawer => "authorize.noSaleDrawer",
        ManagerAction.NegativeStockSale => "authorize.negativeStockSale",
        ManagerAction.DeleteProduct => "authorize.deleteProduct",
        ManagerAction.ChangeExchangeRate => "authorize.changeExchangeRate",
        ManagerAction.ReprintFiscalReceipt => "authorize.reprintFiscalReceipt",
        _ => "authorize.unknown"
    };

    public static string Label(this ManagerAction a) => a switch
    {
        ManagerAction.RemoveCartLine => "Retirer une ligne du panier",
        ManagerAction.ClearCart => "Vider le panier",
        ManagerAction.ApplyLargeDiscount => "Remise supérieure au seuil",
        ManagerAction.OverridePrice => "Modification de prix",
        ManagerAction.CancelInvoice => "Annulation de facture",
        ManagerAction.IssueCreditNote => "Émission d'un avoir",
        ManagerAction.ReopenSession => "Réouverture de session",
        ManagerAction.NoSaleDrawer => "Ouverture tiroir sans vente",
        ManagerAction.NegativeStockSale => "Vente en stock négatif",
        ManagerAction.DeleteProduct => "Suppression de produit",
        ManagerAction.ChangeExchangeRate => "Modification taux de change",
        ManagerAction.ReprintFiscalReceipt => "Réimpression fiscale",
        _ => a.ToString()
    };
}