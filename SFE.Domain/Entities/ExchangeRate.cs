using SFE.Domain.Enums;

namespace SFE.Domain.Entities
{
    public class ExchangeRate
    {
        public int Id { get; set; }
        public decimal Rate { get; set; } // 1 USD = X CDF
        public ExchangeRateMode Source { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }
}