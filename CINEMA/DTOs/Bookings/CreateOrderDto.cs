using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "Vui lòng chọn suất chiếu")]
        public int ShowtimeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 ghế")]
        public List<int> SeatIds { get; set; } = new();

        public List<ComboSelectionDto>? Combos { get; set; }

        public string? VoucherCode { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán không được để trống")]
        public string PaymentMethod { get; set; } = "VNPay";
    }
}
