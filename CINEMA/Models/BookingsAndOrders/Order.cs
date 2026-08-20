using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int? CustomerId { get; set; }

    public string? Status { get; set; }

    public string? PaymentMethod { get; set; }

    public decimal? TotalAmount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public decimal? TicketTotal { get; set; }

    public decimal? ComboTotal { get; set; }
  
    public virtual Customer? Customer { get; set; }
    public string? VoucherCode { get; set; }

    public decimal? DiscountAmount { get; set; }
    public virtual ICollection<OrderCombo> OrderCombos { get; set; } = new List<OrderCombo>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
