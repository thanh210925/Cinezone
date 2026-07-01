namespace CINEMA.Models
{
    public class UserSearchLog
    {
        public long Id { get; set; }
        public int? CustomerId { get; set; }
        public string Keyword { get; set; } = null!;
        public int? ResultCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Customer? Customer { get; set; }
    }
}