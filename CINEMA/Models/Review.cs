using System;

namespace CINEMA.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int MovieId { get; set; }
        public int CustomerId { get; set; }
        public int? OrderId { get; set; }

        public int Rating { get; set; } // 1 to 5
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsHidden { get; set; } = false;

        // Likes count
        public int LikesCount { get; set; } = 0;

        // Admin reply
        public string? AdminReply { get; set; }

        // Report status or comment report count/reason
        public string? ReportReason { get; set; }
        public bool HasReport { get; set; } = false;

        // Navigation properties
        public virtual Movie? Movie { get; set; }
        public virtual Customer? Customer { get; set; }
        public virtual Order? Order { get; set; }
    }
}
