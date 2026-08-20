using System.Collections.Generic;

namespace CINEMA.DTOs
{
    public class EmployeeTimesheetDto
    {
        public int AdminId { get; set; }
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public Dictionary<int, string> DailyStatus { get; set; } = new();
        public int TotalShifts { get; set; }
        public int TotalWorked { get; set; }
        public int TotalLateDays { get; set; }
        public int TotalEarlyDays { get; set; }
        public int TotalLeaveDays { get; set; }
        public int TotalAbsentDays { get; set; }
        public double TotalLateMinutes { get; set; }
        public double TotalEarlyMinutes { get; set; }
    }
}
