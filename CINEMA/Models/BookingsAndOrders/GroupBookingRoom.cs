using System;
using System.Collections.Generic;

namespace CINEMA.Models
{
    public class GroupBookingRoom
    {
        public string RoomId { get; set; } = null!;
        public int ShowtimeId { get; set; }
        public int CreatedBy { get; set; }
        public string Status { get; set; } = "Waiting";
        public int MaxMembers { get; set; } = 10;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Showtime Showtime { get; set; } = null!;
        public virtual Customer Creator { get; set; } = null!;
        public virtual ICollection<GroupBookingMember> Members { get; set; } = new List<GroupBookingMember>();
    }
}
