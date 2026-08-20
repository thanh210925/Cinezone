namespace CINEMA.DTOs
{
    public class MovieDetailDto : MovieSummaryDto
    {
        public DateOnly? EndDate { get; set; }
        public string? Language { get; set; }
        public string? Country { get; set; }
        public bool? IsActive { get; set; }
        public List<ReviewDto> RecentReviews { get; set; } = new();
    }
}
