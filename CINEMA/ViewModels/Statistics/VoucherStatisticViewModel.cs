namespace CINEMA.ViewModels
{
    public class VoucherStatisticViewModel
    {
        public string VoucherCode { get; set; } = "";

        public int UsedCount { get; set; }

        public decimal TotalDiscount { get; set; }

        public double UsageRate { get; set; }
    }
}