using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFE.Domain.Enums
{
    /// <summary>
    /// Type de la taxe spécifique appliquée à un article.
    /// </summary>
    public enum SpecificTaxType
    {
        /// <summary>Pas de taxe spécifique</summary>
        None = 0,

        /// <summary>Pourcentage appliqué sur le montant HT (ex : 10% du HT)</summary>
        Percentage = 1,

        /// <summary>Montant fixe par unité vendue (ex : 50 CDF/unité)</summary>
        FixedPerUnit = 2
    }
}
