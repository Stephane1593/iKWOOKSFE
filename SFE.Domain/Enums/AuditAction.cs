namespace SFE.Domain.Enums;

public enum AuditAction
{
    // ═══ Authentication ═══
    UserLogin = 100,
    UserLogout = 101,
    UserLoginFailed = 102,

    // ═══ Invoicing ═══
    InvoiceNormalized = 200,
    InvoicePrinted = 201,
    InvoiceDuplicated = 202,
    CreditNoteNormalized = 203,
    AdvanceInvoiceNormalized = 204,
    ProformaCreated = 205,        // 🆕
    ProformaConverted = 206,      // 🆕
    ProformaCancelled = 207,      // 🆕

    // ═══ Reports ═══
    ReportZGenerated = 300,
    ReportXGenerated = 301,
    ReportAGenerated = 302,
    ReportExported = 303,

    // ═══ Session ═══
    SessionOpened = 400,
    SessionClosed = 401,
    CashDeposit = 402,
    CashWithdrawal = 403,

    // ═══ Products ═══
    ProductCreated = 500,
    ProductUpdated = 501,
    ProductDeleted = 502,

    // ═══ Categories ═══
    CategoryCreated = 510,
    CategoryUpdated = 511,
    CategoryDeleted = 512,

    // ═══ Stock ═══
    StockAdjusted = 600,
    StockTransferCreated = 601,
    StockTransferShipped = 602,
    StockTransferReceived = 603,
    StockTransferCancelled = 604,
    StockEntry = 605,
    StockExit = 607,
    StockAdjustment = 608,
    StockPhysicalCount = 609,
    StockInitial = 610,
    StockSaleDecrement = 611,
    StockCreditReturn = 612,
    TransferCreated = 613,
    TransferShipped = 614,
    TransferReceived = 615,
    TransferCancelled = 616,

    // ═══ Clients ═══
    ClientCreated = 700,
    ClientUpdated = 701,
    ClientDeleted = 702,

    // ═══ Users & Roles ═══
    UserCreated = 800,
    UserUpdated = 801,
    UserDeleted = 802,
    RoleCreated = 810,
    RoleUpdated = 811,
    RoleDeleted = 812,
    UserActivated = 813,
    UserDeactivated = 814,


    // ═══ Settings ═══
    SettingsUpdated = 900,
    CompanyUpdated = 901,
    PosCreated = 902,
    PosUpdated = 903,
    ExchangeRateUpdated = 904,
    PosReactivated = 905,

    // ═══ System / MCF ═══
    McfConnectionLost = 1000,
    McfReconnected = 1001,
    McfSyncRequested = 1002,

    // ═══ POS ═══
    PosDeactivated,

    InvoiceNormalizationFailed,
    InvoiceFiscalDeviceError,
    InvoiceValidationFailed,
    InvoiceSaveFailed,

    // --- Licensing --- (block 1100)
    LicenseTrialIssued = 1100,
    LicenseInstalled = 1101,
    LicenseActivated = 1102,
    LicenseRevokedByPortal = 1103,
    LicenseExpired = 1104,
    LicenseEnteredGrace = 1105,
    LicenseEnteredOffline = 1106,
    LicenseTamperDetected = 1107,
    LicenseFeatureBlocked = 1108,
    LicenseHeartbeatSucceeded = 1109,
    LicenseHeartbeatFailed = 1110,

    // --- Authorization ---
    ManagerAuthorizationGranted = 1200,
    ManagerAuthorizationDenied = 1201,
    ManagerCardIssued = 1202,
    ManagerCardRevoked = 1203,
    ManagerCardAutoRevoked = 1204,
}

public static class AuditActionExtensions
{
    public static string Label(this AuditAction a) => a switch
    {
        AuditAction.UserLogin => "Connexion",
        AuditAction.UserLogout => "Déconnexion",
        AuditAction.UserLoginFailed => "Échec connexion",

        AuditAction.InvoiceNormalized => "Facture normalisée",
        AuditAction.InvoicePrinted => "Facture imprimée",
        AuditAction.InvoiceDuplicated => "Duplicata",
        AuditAction.CreditNoteNormalized => "Avoir normalisé",
        AuditAction.AdvanceInvoiceNormalized => "Acompte normalisé",

        AuditAction.ReportZGenerated => "Rapport Z",
        AuditAction.ReportXGenerated => "Rapport X",
        AuditAction.ReportAGenerated => "Rapport A",
        AuditAction.ReportExported => "Rapport exporté",

        AuditAction.SessionOpened => "Ouverture session",
        AuditAction.SessionClosed => "Clôture session",
        AuditAction.CashDeposit => "Dépôt espèces",
        AuditAction.CashWithdrawal => "Retrait espèces",

        AuditAction.ProductCreated => "Produit créé",
        AuditAction.ProductUpdated => "Produit modifié",
        AuditAction.ProductDeleted => "Produit supprimé",

        AuditAction.CategoryCreated => "Catégorie créée",
        AuditAction.CategoryUpdated => "Catégorie modifiée",
        AuditAction.CategoryDeleted => "Catégorie supprimée",

        AuditAction.StockAdjusted => "Ajustement stock",
        AuditAction.StockTransferCreated => "Transfert créé",
        AuditAction.StockTransferShipped => "Transfert expédié",
        AuditAction.StockTransferReceived => "Transfert reçu",
        AuditAction.StockTransferCancelled => "Transfert annulé",

        AuditAction.ClientCreated => "Client créé",
        AuditAction.ClientUpdated => "Client modifié",

        AuditAction.UserCreated => "Utilisateur créé",
        AuditAction.UserUpdated => "Utilisateur modifié",
        AuditAction.UserDeleted => "Utilisateur supprimé",
        AuditAction.RoleCreated => "Rôle créé",
        AuditAction.RoleUpdated => "Rôle modifié",
        AuditAction.RoleDeleted => "Rôle supprimé",

        AuditAction.SettingsUpdated => "Paramètres modifiés",
        AuditAction.CompanyUpdated => "Entreprise modifiée",
        AuditAction.PosCreated => "PDV créé",
        AuditAction.PosUpdated => "PDV modifié",
        AuditAction.ExchangeRateUpdated => "Taux de change MAJ",

        AuditAction.McfConnectionLost => "MCF déconnecté",
        AuditAction.McfReconnected => "MCF reconnecté",
        AuditAction.McfSyncRequested => "Sync MCF demandée",

        AuditAction.InvoiceNormalizationFailed => "Échec normalisation facture",
        AuditAction.InvoiceFiscalDeviceError => "Erreur dispositif fiscal",
        AuditAction.InvoiceValidationFailed => "Échec validation facture",
        AuditAction.InvoiceSaveFailed => "Échec sauvegarde facture",

        AuditAction.ProformaCreated => "Proforma créée",
        AuditAction.ProformaConverted => "Proforma convertie",
        AuditAction.ProformaCancelled => "Proforma annulée",

        AuditAction.LicenseTrialIssued => "Licence d'essai émise",
        AuditAction.LicenseInstalled => "Licence installée",
        AuditAction.LicenseActivated => "Licence activée",
        AuditAction.LicenseRevokedByPortal => "Licence révoquée",
        AuditAction.LicenseExpired => "Licence expirée",
        AuditAction.LicenseEnteredGrace => "Licence en délai de grâce",
        AuditAction.LicenseEnteredOffline => "Licence hors ligne prolongée",
        AuditAction.LicenseTamperDetected => "Anomalie licence détectée",
        AuditAction.LicenseFeatureBlocked => "Fonctionnalité bloquée",
        AuditAction.LicenseHeartbeatSucceeded => "Heartbeat OK",
        AuditAction.LicenseHeartbeatFailed => "Heartbeat échec",

        AuditAction.ManagerAuthorizationGranted => "Autorisation manager accordée",
        AuditAction.ManagerAuthorizationDenied => "Autorisation manager refusée",


        _ => a.ToString()
    };
}