using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Theater
{

    public int TheaterId { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? GoogleMapUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsActive { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Auditorium> Auditoria { get; set; } = new List<Auditorium>();
}
