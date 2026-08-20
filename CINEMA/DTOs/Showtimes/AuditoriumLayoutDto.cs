namespace CINEMA.DTOs
{
    public class AuditoriumLayoutDto
    {
        public int ShowtimeId { get; set; }
        public int AuditoriumId { get; set; }
        public string AuditoriumName { get; set; } = null!;
        public string MovieTitle { get; set; } = null!;
        public DateTime? StartTime { get; set; }
        public decimal BasePrice { get; set; }
        public List<SeatStatusDto> Seats { get; set; } = new();
    }
}
