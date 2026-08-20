namespace CINEMA.DTOs
{
    public class SeatStatusDto
    {
        public int SeatId { get; set; }
        public string? RowLabel { get; set; }
        public int? SeatNumber { get; set; }
        public string SeatName => $"{RowLabel}{SeatNumber}";
        public string? SeatType { get; set; }
        public decimal Price { get; set; }
        public bool IsBooked { get; set; }
        public bool IsHeldByOther { get; set; }
    }
}
