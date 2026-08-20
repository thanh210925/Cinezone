using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Seat
{
    public int SeatId { get; set; }

    public int? AuditoriumId { get; set; }

    public string? RowLabel { get; set; }

    public int? SeatNumber { get; set; }

    public string? SeatType { get; set; }

    public bool? IsActive { get; set; }

    public virtual Auditorium? Auditorium { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
