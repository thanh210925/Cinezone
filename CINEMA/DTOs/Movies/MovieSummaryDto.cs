namespace CINEMA.DTOs
{
    public class MovieSummaryDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? Duration { get; set; }
        public string? PosterUrl { get; set; }
        public string? TrailerUrl { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public string? AgeRating { get; set; }
        public double RatingAverage { get; set; }
        public int VoteCount { get; set; }
        public List<string> Genres { get; set; } = new();
        public bool IsShowing { get; set; }
    }
}
