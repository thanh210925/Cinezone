using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Ticket
{
    public int TicketId { get; set; }

    public int? ShowtimeId { get; set; }

    public int? SeatId { get; set; }

    public int? CustomerId { get; set; }

    public decimal? Price { get; set; }

    public string? Status { get; set; }

    public DateTime? BookedAt { get; set; }

    public string? PaymentStatus { get; set; }

    public int? OrderId { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Seat? Seat { get; set; }

    public virtual Showtime? Showtime { get; set; }

    public virtual ICollection<TicketCombo> TicketCombos { get; set; } = new List<TicketCombo>();
}
