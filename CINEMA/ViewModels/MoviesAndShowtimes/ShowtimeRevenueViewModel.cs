namespace CINEMA.ViewModels
{
    public class ShowtimeStatisticViewModel
    {
        public int ShowtimeId { get; set; }

        public string MovieName { get; set; } = "";

        public string AuditoriumName { get; set; } = "";

        public DateTime StartTime { get; set; }

        public int TicketCount { get; set; }

        public decimal Revenue { get; set; }
    }
}