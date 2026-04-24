namespace SFE.Domain.Enums;

public enum AuditModule
{
    Authentication,
    Invoicing,
    Reports,
    Session,
    Products,
    Stock,
    Clients,
    Users,
    PointOfSale,
    Settings,
    System
}

public static class AuditModuleExtensions
{
    public static string Label(this AuditModule m) => m switch
    {
        AuditModule.Authentication => "Authentification",
        AuditModule.Invoicing => "Facturation",
        AuditModule.Reports => "Rapports",
        AuditModule.Session => "Session",
        AuditModule.Products => "Produits",
        AuditModule.Stock => "Stock",
        AuditModule.Clients => "Clients",
        AuditModule.Users => "Utilisateurs",
        AuditModule.Settings => "Paramètres",
        AuditModule.System => "Système",
        AuditModule.PointOfSale => "PDV",
        _ => m.ToString()
    };

    public static string Icon(this AuditModule m) => m switch
    {
        AuditModule.Authentication => "ShieldAccountOutline",
        AuditModule.Invoicing => "ReceiptTextOutline",
        AuditModule.Reports => "ChartBoxOutline",
        AuditModule.Session => "CashRegister",
        AuditModule.Products => "PackageVariantClosed",
        AuditModule.Stock => "WarehouseOutline",
        AuditModule.Clients => "AccountGroupOutline",
        AuditModule.Users => "AccountCogOutline",
        AuditModule.Settings => "CogOutline",
        AuditModule.System => "ServerNetworkOutline",
        _ => "InformationOutline"
    };
}