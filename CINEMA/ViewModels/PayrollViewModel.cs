namespace CINEMA.ViewModels
{
    public class PayrollViewModel
    {
        public string? FullName { get; set; }
        public string? EmployeeCode { get; set; }
        public int TotalDays { get; set; } // Tổng ngày công đã được duyệt
        // Thêm dòng này để lưu chuỗi "X giờ Y phút Z giây"
        public string TotalTimeFormatted { get; set; }
    }
}