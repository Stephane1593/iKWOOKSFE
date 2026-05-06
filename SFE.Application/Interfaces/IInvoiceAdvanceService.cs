using SFE.Domain.Entities;
using SFE.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFE.Application.Interfaces
{
    /// <summary>
    /// Builds and validates advance-invoice chains (FT/ET → FV/EV).
    /// Stateless: pure business logic, no I/O.
    /// </summary>
    public interface IInvoiceAdvanceService
    {
        /// <summary>
        /// Prepares an FT/ET draft from an order context.
        /// The resulting invoice has ONE synthetic line "Acompte sur commande"
        /// at the dominant tax group, with TotalTTC == advanceAmount.
        /// </summary>
        Invoice BuildAdvanceInvoice(AdvanceBuildContext ctx);

        /// <summary>
        /// Validates an advance chain BEFORE finalizing the FV.
        /// Throws AdvanceChainException with the first detected issue.
        /// </summary>
        void ValidateChain(
            Invoice finalInvoice,
            IReadOnlyList<Invoice> previousAdvances);

        /// <summary>
        /// Builds the synthetic payment lines representing prior advances
        /// to be inserted in the final FV/EV's Payments collection.
        /// </summary>
        IReadOnlyList<InvoicePayment> BuildAdvancePayments(
            IReadOnlyList<Invoice> previousAdvances);
    }

    public class AdvanceBuildContext
    {
        public string AdvanceGroupId { get; set; } = string.Empty;
        public bool IsExport { get; set; }
        public decimal OrderTotal { get; set; }
        public decimal PreviousAdvancesTotal { get; set; }
        public decimal AdvanceAmount { get; set; }
        public TaxGroup DominantTaxGroup { get; set; } = TaxGroup.B;
        public decimal DominantTaxRate { get; set; } = 16m;
        public PriceMode PriceMode { get; set; } = PriceMode.TTC;
        public string ClientName { get; set; } = string.Empty;
        public string ClientNIF { get; set; } = string.Empty;
        public ClientType ClientType { get; set; } = ClientType.PP;
        public int PointOfSaleId { get; set; }
        public string OperatorId { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public string OrderReference { get; set; } = string.Empty;
        public List<InvoicePayment> Payments { get; set; } = new();
    }

    public class AdvanceChainException : Exception
    {
        public AdvanceChainException(string message) : base(message) { }
    }
}
