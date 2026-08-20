using System.ComponentModel.DataAnnotations;

namespace CINEMA.Models
{
    public class UserActivityLog
    {
        [Key]
        public long LogId { get; set; }
        public int? CustomerId { get; set; }
        public string? SessionId { get; set; }
        public string ActivityType { get; set; } = null!;
        public int? MovieId { get; set; }
        public int? GenreId { get; set; }
        public string? Metadata { get; set; }
        public string? DeviceType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Customer? Customer { get; set; }
        public Movie? Movie { get; set; }
        public Genre? Genre { get; set; }
    }
}