namespace SFE.Domain.Enums;

/// <summary>
/// État connexion serveur MCF — spec MCF C2h champ STA
/// </summary>
public enum McfConnectionStatus
{
    DIS, // Non connecté au réseau
    CON, // Connecté, pas d'envoi en cours
    TRA, // Envoi de données en cours
    RES  // Redémarrage en cours
}