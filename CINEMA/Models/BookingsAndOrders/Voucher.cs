namespace CINEMA.Models
{
    public class Voucher
    {
        public int VoucherId { get; set; }
        public string Code { get; set; } = "";

        public double? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }

        public decimal MinOrderValue { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public int Quantity { get; set; }
        public int UsedCount { get; set; }

        public bool IsActive { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? VoucherConditionId { get; set; }
        public virtual VoucherCondition? VoucherCondition { get; set; }
        public string? TermsAndConditions { get; set; }
    }
}
