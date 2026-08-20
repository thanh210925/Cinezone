using System.ComponentModel.DataAnnotations;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    public class CreateShowtimeApiDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required]
        public int AuditoriumId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public decimal BasePrice { get; set; } = 80000;

        public string? Language { get; set; }
    }

    [ApiController]
    [Route("api/admin/showtimes")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,Manager")]
    public class AdminShowtimesApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminShowtimesApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tạo suất chiếu mới (Admin)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateShowtime([FromBody] CreateShowtimeApiDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var movie = await _context.Movies.FindAsync(model.MovieId);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            var auditorium = await _context.Auditoriums.FindAsync(model.AuditoriumId);
            if (auditorium == null)
                return NotFound(new { message = "Không tìm thấy phòng chiếu." });

            int duration = movie.Duration ?? 120;
            DateTime endTime = model.StartTime.AddMinutes(duration + 15);

            // Kiểm tra trùng lịch chiếu trong phòng
            var conflict = await _context.Showtimes
                .AnyAsync(s => s.AuditoriumId == model.AuditoriumId
                        && s.IsActive == true
                        && s.StartTime < endTime
                        && s.EndTime > model.StartTime);

            if (conflict)
            {
                return BadRequest(new { message = "Khung giờ này phòng chiếu đã có suất chiếu khác." });
            }

            var showtime = new Showtime
            {
                MovieId = model.MovieId,
                AuditoriumId = model.AuditoriumId,
                StartTime = model.StartTime,
                EndTime = endTime,
                BasePrice = model.BasePrice,
                Language = model.Language ?? movie.Language ?? "Phụ đề",
                IsActive = true
            };

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tạo suất chiếu mới thành công.", showtimeId = showtime.ShowtimeId });
        }

        /// <summary>
        /// Hủy / Vô hiệu hóa suất chiếu (Admin)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelShowtime(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
                return NotFound(new { message = "Không tìm thấy suất chiếu." });

            showtime.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã hủy suất chiếu thành công." });
        }
    }
}
