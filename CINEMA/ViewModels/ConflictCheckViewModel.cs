namespace CINEMA.ViewModels
{
    public class ConflictCheckRequest
    {
        public int Index { get; set; }
        public int AuditoriumId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsSelected { get; set; }
    }

    public class ConflictCheckResult
    {
        public int Index { get; set; }
        public bool HasConflict { get; set; }
        public string? ConflictWith { get; set; }
        public bool IsBatchConflict { get; set; }
    }

    /// <summary>DTO nhận từ Wizard V3 qua AJAX JSON POST</summary>
    public class ShowtimeJsonDto
    {
        public int MovieId { get; set; }
        public int AuditoriumId { get; set; }
        public string StartTime { get; set; } = "";   // ISO string: "2026-07-20T09:00:00"
        public string EndTime { get; set; } = "";
        public double BasePrice { get; set; }
        public string Language { get; set; } = "Vietsub";
    }
}

