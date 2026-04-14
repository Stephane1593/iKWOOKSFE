using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFE.Domain.Enums
{
    /// <summary>Comment la taxe spécifique est appliquée sur une facture.</summary>
    public enum TaxSpecificMode
    {
        /// <summary>Calculée sur chaque ligne individuellement.</summary>
        PerArticle,
        /// <summary>Différée : calculée une seule fois sur le sous-total regroupé.</summary>
        OnTotal
    }
}
