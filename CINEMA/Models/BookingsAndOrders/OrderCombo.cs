using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class OrderCombo
{
    public int OrderComboId { get; set; }

    public int? OrderId { get; set; }

    public int? ComboId { get; set; }

    public int? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public virtual Combo? Combo { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<TicketCombo> TicketCombos { get; set; } = new List<TicketCombo>();
}
