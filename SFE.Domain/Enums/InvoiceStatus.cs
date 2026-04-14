namespace SFE.Domain.Enums;

public enum InvoiceStatus
{
    Draft,       // En cours de composition
    Pending,     // Envoyée au dispositif fiscal, en attente de confirmation
    Normalized,  // Normalisée — a un Code DEF/DGI
    Cancelled,   // Annulée pendant la normalisation
    Error        // Erreur
}