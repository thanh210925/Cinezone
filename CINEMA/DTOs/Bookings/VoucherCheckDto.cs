using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class VoucherCheckDto
    {
        [Required]
        public string Code { get; set; } = null!;
        public decimal TicketTotal { get; set; }
        public decimal ComboTotal { get; set; }
        public int TicketQuantity { get; set; }
        public int ComboQuantity { get; set; }
    }
}
