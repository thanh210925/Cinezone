using System;

namespace CINEMA.Models
{
    public class GroupBookingMember
    {
        public int MemberId { get; set; }
        public string RoomId { get; set; } = null!;
        public int CustomerId { get; set; }
        public int? SeatId { get; set; }
        public int? OrderId { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
        public string Status { get; set; } = "Joined";

        public virtual GroupBookingRoom Room { get; set; } = null!;
        public virtual Customer Customer { get; set; } = null!;
        public virtual Seat? Seat { get; set; }
        public virtual Order? Order { get; set; }
    }
}
