using SFE.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

public static class PosSelectionHelper
{
    /// <summary>
    /// Returns the POS assigned to the current user if it exists in the available list,
    /// otherwise returns the first available POS, or null.
    /// </summary>
    public static PointOfSale SelectBestPos(
        IEnumerable<PointOfSale> availablePos,
        int? userPointOfSaleId,
        int? previousPointOfSaleId = null)   // 👈 add default
    {
        if (availablePos == null || !availablePos.Any())
            return null;

        // 1️⃣ User-assigned POS
        if (userPointOfSaleId.HasValue)
        {
            var userPos = availablePos.FirstOrDefault(p => p.Id == userPointOfSaleId.Value);
            if (userPos != null) return userPos;
        }

        // 2️⃣ Previous selection
        if (previousPointOfSaleId.HasValue)
        {
            var prevPos = availablePos.FirstOrDefault(p => p.Id == previousPointOfSaleId.Value);
            if (prevPos != null) return prevPos;
        }

        // 3️⃣ Fallback
        return availablePos.First();
    }
}