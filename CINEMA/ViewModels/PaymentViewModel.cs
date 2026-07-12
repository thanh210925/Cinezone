using System;
using System.Collections.Generic;
using CINEMA.Models;

namespace CINEMA.ViewModels
{
    public class ComboViewModel
    {
        public int OrderComboId { get; set; }
        public int ComboId { get; set; }
        public string ComboName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class PaymentViewModel
    {
        // 👉 Thông tin khách hàng
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }

        // 👉 Thông tin phim & suất chiếu
        public int MovieId { get; set; }
        public int ShowtimeId { get; set; }
        public string MovieTitle { get; set; }
        public string Showtime { get; set; }
        public string Auditorium { get; set; }

        // ⭐👉 THÔNG TIN RẠP CHIẾU — THÊM ĐÚNG NƠI NÀY
        public string TheaterName { get; set; }
        public string TheaterAddress { get; set; }
        public string TheaterPhone { get; set; }

        // 👉 Ghế đã chọn
        public List<string> SelectedSeats { get; set; } = new List<string>();

        // 👉 Combo
        public List<ComboViewModel> Combos { get; set; } = new List<ComboViewModel>();

        // 👉 Loại vé
        public int AdultTickets { get; set; }
        public int ChildTickets { get; set; }
        public int StudentTickets { get; set; }

        // 👉 Tổng tiền
        public decimal TotalPrice { get; set; }

        // 👉 Không cần thiết nhưng bé đang dùng
        public string PaymentMethod { get; set; }
        public string PaymentImageUrl { get; set; }
        public decimal TicketTotal { get; set; }
        public string VoucherCode { get; set; }
        public decimal? DiscountAmount { get; set; }

        // 👉 Thông tin ưu đãi hạng thành viên
        public string MembershipLevel { get; set; }
        public decimal MembershipDiscountPercent { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal OriginalPrice { get; set; }
    }

    public class UpsellOffer
    {
        public int CurrentComboId { get; set; }
        public string CurrentComboName { get; set; }
        public int UpgradedComboId { get; set; }
        public string UpgradedComboName { get; set; }
        public decimal PriceDiff { get; set; }
        public decimal NewPrice { get; set; }
    }
}
