namespace CINEMA.DTOs
{
    public class ReviewDto
    {
        public int ReviewId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string? CustomerAvatar { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
