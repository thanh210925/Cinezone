using System;

namespace CINEMA.Models
{
    public class ChatMessage
    {
        public int ChatMessageId { get; set; }
        public int CustomerId { get; set; }
        public string MessageText { get; set; } = null!;
        public bool IsFromCustomer { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        // Quan hệ với Customer
        public virtual Customer Customer { get; set; } = null!;
    }
}
