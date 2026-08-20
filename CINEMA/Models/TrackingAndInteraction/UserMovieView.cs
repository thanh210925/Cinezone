namespace CINEMA.Models
{
    public class UserMovieView
    {
        public long Id { get; set; }
        public int CustomerId { get; set; }
        public int MovieId { get; set; }
        public int ViewCount { get; set; } = 1;
        public DateTime LastViewedAt { get; set; } = DateTime.Now;

        public Customer? Customer { get; set; }
        public Movie? Movie { get; set; }
    }
}