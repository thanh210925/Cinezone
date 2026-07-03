namespace CINEMA.ViewModels
{
    public class PayrollViewModel
    {
        public int AdminId { get; set; } // PHẢI CÓ ĐỂ TRUYỀN VÀO MODAL
        public string FullName { get; set; }
        public string EmployeeCode { get; set; }
        public int TotalDays { get; set; }
        public string TotalTimeFormatted { get; set; }
    }
}