namespace SFE.Domain.Enums;

/// <summary>
/// Type de dispositif électronique fiscal.
/// Dans les Paramètres, l'utilisateur bascule entre ces deux modes.
/// </summary>
public enum DeviceType
{
    /// <summary>e-MCF : API REST distante hébergée par la DGI</summary>
    EMcf = 0,

    /// <summary>MCF : appareil physique connecté via port série RS232/USB</summary>
    Mcf = 1
}