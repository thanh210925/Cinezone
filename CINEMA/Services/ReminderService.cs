using CINEMA.Models;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Services
{
    public interface IReminderService
    {
        Task<int> SendShowtimeRemindersAsync(string baseUrl);
    }

    public class ReminderService : IReminderService
    {
        private readonly CinemaContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(CinemaContext context, IEmailService emailService, ILogger<ReminderService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Tự động gửi Email nhắc nhở cho tất cả vé sắp chiếu trong vòng 30-60 phút tới
        /// </summary>
        public async Task<int> SendShowtimeRemindersAsync(string baseUrl)
        {
            var now = DateTime.Now;
            var windowStart = now.AddMinutes(10);
            var windowEnd = now.AddMinutes(60);

            var upcomingOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets).ThenInclude(t => t.Showtime).ThenInclude(s => s!.Movie)
                .Where(o => (o.Status == "Đã thanh toán" || o.Status == "Completed" || o.Status == "Paid")
                    && o.Tickets.Any(t => t.Showtime != null && t.Showtime.StartTime >= windowStart && t.Showtime.StartTime <= windowEnd))
                .ToListAsync();

            int sentCount = 0;

            foreach (var order in upcomingOrders)
            {
                if (order.Customer != null && !string.IsNullOrEmpty(order.Customer.Email))
                {
                    try
                    {
                        await _emailService.SendPaymentReminderEmailAsync(order.OrderId, baseUrl);
                        sentCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send showtime reminder to {Email} for order #{OrderId}", order.Customer.Email, order.OrderId);
                    }
                }
            }

            return sentCount;
        }
    }
}
