using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Showtime
{
    public int ShowtimeId { get; set; }

    public int? MovieId { get; set; }

    public int? AuditoriumId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public decimal? BasePrice { get; set; }

    public string? Language { get; set; }

    public bool? IsActive { get; set; }

    public virtual Auditorium? Auditorium { get; set; }

    public virtual Movie? Movie { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public virtual ICollection<Combo> Combos { get; set; } = new List<Combo>();
}
