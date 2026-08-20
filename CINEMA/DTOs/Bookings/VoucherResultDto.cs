namespace CINEMA.DTOs
{
    public class VoucherResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = null!;
        public string? Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalTotal { get; set; }
    }
}
