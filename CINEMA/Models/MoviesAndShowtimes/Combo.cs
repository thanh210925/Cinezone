using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Combo
{
    public int ComboId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<OrderCombo> OrderCombos { get; set; } = new List<OrderCombo>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
