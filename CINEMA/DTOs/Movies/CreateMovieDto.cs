using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class CreateMovieDto
    {
        [Required(ErrorMessage = "Tên phim không được để trống")]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? Duration { get; set; }
        public string? TrailerUrl { get; set; }
        public string? PosterUrl { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Language { get; set; }
        public string? Country { get; set; }
        public string? AgeRating { get; set; }
        public List<int> GenreIds { get; set; } = new();
    }
}
