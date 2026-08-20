namespace CINEMA.DTOs
{
    public class ShowtimeDto
    {
        public int ShowtimeId { get; set; }
        public int? MovieId { get; set; }
        public string? MovieTitle { get; set; }
        public string? PosterUrl { get; set; }
        public int? AuditoriumId { get; set; }
        public string? AuditoriumName { get; set; }
        public string? TheaterName { get; set; }
        public string? BranchName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? BasePrice { get; set; }
        public string? Language { get; set; }
    }
}
