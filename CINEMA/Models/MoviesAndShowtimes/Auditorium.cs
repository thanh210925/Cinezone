using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Auditorium
{
    public int AuditoriumId { get; set; }

    public int? TheaterId { get; set; }

    public string? Name { get; set; }

    public int? SeatRows { get; set; }

    public int? SeatCols { get; set; }

    public string? ScreenType { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();

    public virtual Theater? Theater { get; set; }
}
