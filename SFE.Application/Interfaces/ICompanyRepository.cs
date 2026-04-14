using SFE.Domain.Entities;

namespace SFE.Application.Interfaces;

/// <summary>
/// Repository spécifique pour l'entreprise.
/// En mode Standalone, il n'y a qu'une seule Company.
/// </summary>
public interface ICompanyRepository : IRepository<Company>
{
    /// <summary>
    /// Récupère l'entreprise unique (la première, ou null si pas encore configurée).
    /// </summary>
    Task<Company?> GetCurrentCompanyAsync();

    /// <summary>
    /// Récupère l'entreprise avec tous ses Points de Vente.
    /// </summary>
    Task<Company?> GetWithPointsOfSaleAsync(int companyId);
}