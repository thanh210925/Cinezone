using System;

namespace CINEMA.Models
{
    public class MovieFollower
    {
        public int MovieFollowerId { get; set; }

        public int MovieId { get; set; }

        public int CustomerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsNotified { get; set; } = false;

        public DateTime? NotifiedAt { get; set; }

        public virtual Movie? Movie { get; set; }

        public virtual Customer? Customer { get; set; }
    }
}
