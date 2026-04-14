namespace SFE.Domain.Enums;

public enum ReportType
{
    A,
    X, // Rapport intermédiaire (lecture seule, ne remet pas les compteurs à zéro)
    Z  // Rapport de clôture journalière (remet les compteurs à zéro)
}