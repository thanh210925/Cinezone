using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class TicketCombo
{
    public int TicketId { get; set; }

    public int OrderComboId { get; set; }

    public int? Quantity { get; set; }

    public virtual OrderCombo OrderCombo { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
