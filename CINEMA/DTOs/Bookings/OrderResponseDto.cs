namespace CINEMA.DTOs
{
    public class OrderResponseDto
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = null!;
        public string PaymentMethod { get; set; } = null!;
        public decimal TicketTotal { get; set; }
        public decimal ComboTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PaymentUrl { get; set; }
        public string? MovieTitle { get; set; }
        public string? AuditoriumName { get; set; }
        public DateTime? ShowtimeStart { get; set; }
        public List<string> SeatNames { get; set; } = new();
        public List<string> ComboNames { get; set; } = new();
        public string? QrCodeData { get; set; }
    }
}
