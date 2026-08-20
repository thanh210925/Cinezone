namespace CINEMA.DTOs
{
    public class FaceCheckinResultDto
    {
        public bool IsMatched { get; set; }
        public int? AdminId { get; set; }
        public string? FullName { get; set; }
        public string? EmployeeCode { get; set; }
        public double ConfidencePercent { get; set; }
        public DateTime CheckInTime { get; set; }
        public int? AttendanceId { get; set; }
        public string Message { get; set; } = null!;
    }
}
