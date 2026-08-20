using CINEMA.Models;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Services
{
    public interface INotificationService
    {
        Task<bool> ToggleFollowMovieAsync(int customerId, int movieId);
        Task<bool> IsFollowingMovieAsync(int customerId, int movieId);
        Task<int> NotifyFollowersWhenShowtimeOpenedAsync(int movieId, string baseUrl);
    }

    public class NotificationService : INotificationService
    {
        private readonly CinemaContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(CinemaContext context, IEmailService emailService, ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<bool> ToggleFollowMovieAsync(int customerId, int movieId)
        {
            var follower = await _context.MovieFollowers
                .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.MovieId == movieId);

            if (follower != null)
            {
                _context.MovieFollowers.Remove(follower);
                await _context.SaveChangesAsync();
                return false; // Unfollowed
            }
            else
            {
                _context.MovieFollowers.Add(new MovieFollower
                {
                    CustomerId = customerId,
                    MovieId = movieId,
                    CreatedAt = DateTime.Now,
                    IsNotified = false
                });
                await _context.SaveChangesAsync();
                return true; // Followed
            }
        }

        public async Task<bool> IsFollowingMovieAsync(int customerId, int movieId)
        {
            return await _context.MovieFollowers
                .AnyAsync(f => f.CustomerId == customerId && f.MovieId == movieId);
        }

        public async Task<int> NotifyFollowersWhenShowtimeOpenedAsync(int movieId, string baseUrl)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null) return 0;

            var pendingFollowers = await _context.MovieFollowers
                .Include(f => f.Customer)
                .Where(f => f.MovieId == movieId && !f.IsNotified)
                .ToListAsync();

            if (!pendingFollowers.Any()) return 0;

            int sentCount = 0;
            string movieUrl = $"{baseUrl}/Movie/Details/{movieId}";
            string subject = $"🎬 PHIM HOT MỞ BÁN VÉ: {movie.Title} tại CineZone!";

            foreach (var follower in pendingFollowers)
            {
                if (follower.Customer != null && !string.IsNullOrEmpty(follower.Customer.Email))
                {
                    try
                    {
                        string body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; background: #0f172a; color: #ffffff;'>
                            <h2 style='color: #e11d48;'>🎬 CINEZONE NOTIFICATION</h2>
                            <p>Xin chào <strong>{follower.Customer.FullName ?? "Khách hàng"}</strong>,</p>
                            <p>Bộ phim bạn đang theo dõi: <strong style='color: #f59e0b; font-size: 18px;'>{movie.Title}</strong> vừa chính thức <strong>MỞ BÁN VÉ</strong> suất chiếu đầu tiên tại CineZone!</p>
                            <p>Hãy nhanh tay chọn ghế đẹp nhất bằng cách bấm vào link bên dưới:</p>
                            <p style='margin-top: 25px;'>
                                <a href='{movieUrl}' style='background: #e11d48; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: bold;'>Đặt Vé Ngay</a>
                            </p>
                            <hr style='border-color: #334155; margin-top: 30px;'>
                            <p style='font-size: 12px; color: #94a3b8;'>Cảm ơn bạn đã tin tưởng và sử dụng dịch vụ của CineZone.</p>
                        </div>";

                        await _emailService.SendEmailAsync(follower.Customer.Email, subject, body);
                        follower.IsNotified = true;
                        follower.NotifiedAt = DateTime.Now;
                        sentCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send open showtime notification to {Email} for Movie #{MovieId}", follower.Customer.Email, movieId);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return sentCount;
        }
    }
}
