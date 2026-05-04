namespace SFE.Domain.Enums;

/// <summary>
/// Type de dispositif électronique fiscal.
/// Dans les Paramètres, l'utilisateur bascule entre ces deux modes.
/// </summary>
public enum DeviceType
{
    /// <summary>e-MCF only: API REST distante hébergée par la DGI</summary>
    EMcf = 0,

    /// <summary>MCF only: appareil physique connecté via port série RS232/USB</summary>
    Mcf = 1,

    /// <summary>Hybrid: try e-MCF first, fallback to MCF if unavailable</summary>
    Hybrid = 2
}