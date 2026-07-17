using System;
using System.Collections.Generic;

namespace CINEMA.ViewModels
{
    public class RevenueDashboardViewModel
    {
        // ==========================================
        // 1. TỔNG QUAN & KPI TRUNG BÌNH ĐƠN (AOV)
        // ==========================================
        public decimal TotalRevenue { get; set; }
        public int TotalTickets { get; set; }

        public decimal TodayRevenue { get; set; }
        public int TodayTickets { get; set; }

        public decimal MonthRevenue { get; set; }
        public int MonthTickets { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // --- KPI Giá trị đơn hàng trung bình (AOV) ---
        public decimal AvgOrderValue { get; set; }
        public decimal AvgOrderValueToday { get; set; }
        public decimal AvgOrderValueMonth { get; set; }
        public decimal AvgOrderValueQuarter { get; set; }
        public decimal AvgOrderValueYear { get; set; }
        public decimal RevenueCurrentMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }

        // --- Trạng thái đơn hàng ---
        public int PaidOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CancelledOrders { get; set; }

        // ==========================================
        // 2. BIỂU ĐỒ THEO THỜI GIAN
        // ==========================================
        // --- Theo ngày ---
        public List<string> LabelsByDate { get; set; } = new();
        public List<decimal> RevenueByDate { get; set; } = new();
        public List<int> TicketCountByDate { get; set; } = new();
        public List<int> OrderCountByDate { get; set; } = new();

        // --- Theo tháng ---
        public List<string> LabelsByMonth { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();
        public List<int> TicketCountByMonth { get; set; } = new();

        // --- Theo năm ---
        public List<string> LabelsByYear { get; set; } = new();
        public List<decimal> RevenueByYear { get; set; } = new();
        public List<int> TicketCountByYear { get; set; } = new();

        // ==========================================
        // 3. THỐNG KÊ PHIM & DANH HIỆU (BẢNG VÀNG)
        // ==========================================
        public List<string> MovieLabels { get; set; } = new();
        public List<decimal> RevenueByMovie { get; set; } = new();
        public List<string> MovieTicketLabels { get; set; } = new();
        public List<int> TicketsByMovie { get; set; } = new();

        // --- Danh hiệu hiệu năng phim ---
        public string TopSellingMovie { get; set; } = string.Empty;
        public int TopSellingTickets { get; set; }
        public string LeastSellingMovie { get; set; } = string.Empty;
        public int LeastSellingTickets { get; set; }
        public string TopRevenueMovie { get; set; } = string.Empty;
        public decimal TopRevenueAmount { get; set; }

        // --- Trạng thái phim & Thể loại ---
        public int NowShowingCount { get; set; }
        public int ComingSoonCount { get; set; }
        public List<GenreStatisticViewModel> GenreStatistics { get; set; } = new();

        // ==========================================
        // 4. THỐNG KÊ COMBO CHI TIẾT & KPI NÂNG CAO
        // ==========================================
        public decimal ComboRevenue { get; set; }
        public int ComboSold { get; set; }

        // --- Danh hiệu Combo ---
        public string? BestSellingCombo { get; set; }
        public int BestSellingQuantity { get; set; }
        public string? WorstSellingCombo { get; set; }
        public int WorstSellingQuantity { get; set; }

        // --- KPI hiệu suất bán Combo ---
        public double ComboAttachRate { get; set; }         // Tỷ lệ đơn hàng có mua combo
        public double ComboRevenueRate { get; set; }        // Tỷ trọng doanh thu combo trong tổng doanh thu
        public decimal AverageComboPerOrder { get; set; }    // Chi tiêu combo trung bình trên mỗi đơn hàng

        // --- Danh sách / Biểu đồ Combo ---
        public List<ComboStatisticViewModel> ComboStatistics { get; set; } = new();
        public List<string> ComboPieLabels { get; set; } = new();
        public List<int> ComboPieValues { get; set; } = new();
        public List<string> ComboLabelsByMonth { get; set; } = new();
        public List<decimal> ComboRevenueByMonth { get; set; } = new();
        public List<int> ComboQuantityByMonth { get; set; } = new();

        // --- Top 5 dữ liệu vẽ biểu đồ ---
        public List<string> TopComboLabels { get; set; } = new();
        public List<int> TopComboValues { get; set; } = new();
        public List<string> TopRevenueComboLabels { get; set; } = new();
        public List<decimal> TopRevenueComboValues { get; set; } = new();

        // ==========================================
        // 5. RẠP & SUẤT CHIẾU (NÂNG CAO)
        // ==========================================
        public List<string> CinemaLabels { get; set; } = new();
        public List<decimal> CinemaRevenue { get; set; } = new();

        public List<ShowtimeStatisticViewModel> ShowtimeStatistics { get; set; } = new();
        public List<string> ShowtimeLabels { get; set; } = new();
        public List<int> TicketsByShowtime { get; set; } = new();
        public List<decimal> RevenueByShowtime { get; set; } = new();
        public int TotalVouchers { get; set; }

        public int ActiveVouchers { get; set; }

        public int ExpiredVouchers { get; set; }

        public int UsedVouchers { get; set; }

        public decimal TotalDiscountAmount { get; set; }

        public double VoucherUsageRate { get; set; }

        public List<string> VoucherLabels { get; set; } = new();

        public List<int> VoucherValues { get; set; } = new();
        public List<VoucherStatisticViewModel> VoucherStatistics { get; set; } = new();
        public string BestVoucher { get; set; } = "";

        public int BestVoucherUsage { get; set; }
        public decimal VoucherRevenue { get; set; }

        public List<decimal> VoucherRevenueValues { get; set; } = new();

        // --- Tỷ lệ lấp đầy rạp ---
        public double GlobalOccupancyRate { get; set; }
        public int? SelectedTheaterId { get; set; }
        public List<CINEMA.Models.Ticket> Tickets { get; set; } = new();

        // ==========================================
        // 6. THÀNH PHẦN THỐNG KÊ CHI TIẾT NÂNG CAO
        // ==========================================
        public decimal TicketRevenue { get; set; } // Doanh thu bán vé thực tế
        public decimal GrossComboRevenue { get; set; } // Doanh thu bán combo gộp
        public decimal TotalDiscount { get; set; } // Tổng giá trị giảm giá voucher
        public decimal NetRevenue { get; set; } // Doanh thu ròng sau voucher

        public List<TheaterRevenueViewModel> TheaterRevenues { get; set; } = new();
        public List<PeakHourViewModel> PeakHours { get; set; } = new();
        public List<PeakDayViewModel> PeakDays { get; set; } = new();
        public List<PaymentMethodStatViewModel> PaymentMethodStats { get; set; } = new();
        public List<CustomerSpendViewModel> TopCustomers { get; set; } = new();
        public List<RoomRevenueViewModel> RoomRevenues { get; set; } = new();
        public List<CINEMA.Models.Order> DetailedOrders { get; set; } = new();
    }

    // =====================================================
    // 🔹 CLASS PHỤ THỂ LOẠI (Đã dọn dẹp chuẩn chỉnh)
    // =====================================================
    public class GenreStatisticViewModel
    {
        public string GenreName { get; set; } = string.Empty;
        public int MovieCount { get; set; }
        public double Percentage { get; set; }
    }

    // =====================================================
    // 🔹 CLASS PHỤ PHÂN TÍCH RẠP & PHÒNG CHIẾU
    // =====================================================
    public class TheaterRevenueViewModel
    {
        public string TheaterName { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal ComboRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public double OccupancyRate { get; set; }
    }

    public class RoomRevenueViewModel
    {
        public string RoomName { get; set; } = string.Empty;
        public string TheaterName { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    // =====================================================
    // 🔹 CLASS PHỤ PHÂN TÍCH KHUNG GIỜ VÀ NGÀY VÀNG
    // =====================================================
    public class PeakHourViewModel
    {
        public string TimeSlot { get; set; } = string.Empty;
        public int TicketCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class PeakDayViewModel
    {
        public string DayOfWeek { get; set; } = string.Empty;
        public int TicketCount { get; set; }
        public decimal Revenue { get; set; }
    }

    // =====================================================
    // 🔹 CLASS PHỤ PHƯƠNG THỨC THANH TOÁN
    // =====================================================
    public class PaymentMethodStatViewModel
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    // =====================================================
    // 🔹 CLASS PHỤ THỐNG KÊ KHÁCH HÀNG VIP
    // =====================================================
    public class CustomerSpendViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string MembershipLevel { get; set; } = string.Empty;
        public int TicketsBooked { get; set; }
        public decimal TotalSpent { get; set; }
    }
}