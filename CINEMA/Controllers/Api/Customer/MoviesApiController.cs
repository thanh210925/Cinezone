using System.Security.Claims;
using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    [ApiController]
    [Route("api/movies")]
    public class MoviesApiController : ControllerBase
    {
        private readonly CinemaContext _context;
        private readonly CINEMA.Services.INotificationService _notificationService;

        public MoviesApiController(CinemaContext context, CINEMA.Services.INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Lấy danh sách phim (đang chiếu, sắp chiếu, tìm kiếm theo tên hoặc thể loại)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMovies(
            [FromQuery] string? status = null, // "showing" hoặc "coming"
            [FromQuery] string? search = null,
            [FromQuery] int? genreId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Movies
                .Include(m => m.Genres)
                .AsNoTracking()
                .Where(m => m.IsActive == true);

            var today = DateOnly.FromDateTime(DateTime.Today);

            if (status == "showing")
            {
                query = query.Where(m => m.ReleaseDate <= today && (m.EndDate == null || m.EndDate >= today));
            }
            else if (status == "coming")
            {
                query = query.Where(m => m.ReleaseDate > today);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Title.Contains(search) || (m.Description != null && m.Description.Contains(search)));
            }

            if (genreId.HasValue)
            {
                query = query.Where(m => m.Genres.Any(g => g.GenreId == genreId.Value));
            }

            var totalItems = await query.CountAsync();
            var movies = await query
                .OrderByDescending(m => m.ReleaseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MovieSummaryDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Description = m.Description,
                    Duration = m.Duration,
                    PosterUrl = m.PosterUrl,
                    TrailerUrl = m.TrailerUrl,
                    ReleaseDate = m.ReleaseDate,
                    AgeRating = m.AgeRating,
                    RatingAverage = m.RatingAverage,
                    VoteCount = m.VoteCount,
                    Genres = m.Genres.Select(g => g.Name).ToList(),
                    IsShowing = m.ReleaseDate <= today && (m.EndDate == null || m.EndDate >= today)
                })
                .ToListAsync();

            return Ok(new
            {
                totalItems,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                data = movies
            });
        }

        /// <summary>
        /// Lấy chi tiết thông tin phim theo ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovieDetail(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Genres)
                .Include(m => m.Reviews)
                    .ThenInclude(r => r.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            var today = DateOnly.FromDateTime(DateTime.Today);

            var result = new MovieDetailDto
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Description = movie.Description,
                Duration = movie.Duration,
                PosterUrl = movie.PosterUrl,
                TrailerUrl = movie.TrailerUrl,
                ReleaseDate = movie.ReleaseDate,
                EndDate = movie.EndDate,
                Language = movie.Language,
                Country = movie.Country,
                AgeRating = movie.AgeRating,
                IsActive = movie.IsActive,
                RatingAverage = movie.RatingAverage,
                VoteCount = movie.VoteCount,
                Genres = movie.Genres.Select(g => g.Name).ToList(),
                IsShowing = movie.ReleaseDate <= today && (movie.EndDate == null || movie.EndDate >= today),
                RecentReviews = movie.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .Select(r => new ReviewDto
                    {
                        ReviewId = r.ReviewId,
                        CustomerId = r.CustomerId,
                        CustomerName = r.Customer != null ? r.Customer.FullName : "Khách hàng",
                        CustomerAvatar = r.Customer != null ? (r.Customer.AvatarUrl ?? r.Customer.Avatar) : null,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt
                    })
                    .ToList()
            };

            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách đánh giá của phim
        /// </summary>
        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetMovieReviews(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var query = _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.MovieId == id)
                .AsNoTracking();

            var totalItems = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewDto
                {
                    ReviewId = r.ReviewId,
                    CustomerId = r.CustomerId,
                    CustomerName = r.Customer != null ? r.Customer.FullName : "Khách hàng",
                    CustomerAvatar = r.Customer != null ? (r.Customer.AvatarUrl ?? r.Customer.Avatar) : null,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(new { totalItems, page, pageSize, data = reviews });
        }

        /// <summary>
        /// Đánh giá phim (Yêu cầu đăng nhập)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("{id}/reviews")]
        public async Task<IActionResult> AddReview(int id, [FromBody] CreateReviewDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            var review = new Review
            {
                MovieId = id,
                CustomerId = customerId,
                Rating = model.Rating,
                Comment = model.Comment,
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);

            var reviews = await _context.Reviews.Where(r => r.MovieId == id).ToListAsync();
            movie.VoteCount = reviews.Count + 1;
            movie.RatingAverage = Math.Round((reviews.Sum(r => r.Rating) + model.Rating) / (double)movie.VoteCount, 1);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã gửi đánh giá thành công." });
        }

        /// <summary>
        /// Lấy danh sách tất cả thể loại phim
        /// </summary>
        [HttpGet("genres")]
        public async Task<IActionResult> GetGenres()
        {
            var genres = await _context.Genres
                .AsNoTracking()
                .Select(g => new { g.GenreId, g.Name })
                .ToListAsync();

            return Ok(genres);
        }

        /// <summary>
        /// Bật/Tắt Theo dõi phim (Gửi thông báo khi mở bán vé)
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("{id}/follow")]
        public async Task<IActionResult> ToggleFollowMovie(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            bool isFollowing = await _notificationService.ToggleFollowMovieAsync(customerId, id);
            string msg = isFollowing 
                ? $"Đã bật theo dõi phim '{movie.Title}'. CineZone sẽ gửi email thông báo cho bạn ngay khi mở bán vé!" 
                : $"Đã hủy theo dõi phim '{movie.Title}'.";

            return Ok(new { isFollowing, message = msg });
        }

        /// <summary>
        /// Kiểm tra trạng thái theo dõi phim của người dùng
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("{id}/is-following")]
        public async Task<IActionResult> IsFollowingMovie(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            bool isFollowing = await _notificationService.IsFollowingMovieAsync(customerId, id);
            return Ok(new { isFollowing });
        }
    }
}
