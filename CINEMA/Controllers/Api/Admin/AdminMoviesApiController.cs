using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    [ApiController]
    [Route("api/admin/movies")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,Manager")]
    public class AdminMoviesApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminMoviesApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Thêm phim mới (Admin)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateMovie([FromBody] CreateMovieDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var movie = new Movie
            {
                Title = model.Title,
                Description = model.Description,
                Duration = model.Duration,
                TrailerUrl = model.TrailerUrl,
                PosterUrl = model.PosterUrl,
                ReleaseDate = model.ReleaseDate,
                EndDate = model.EndDate,
                Language = model.Language,
                Country = model.Country,
                AgeRating = model.AgeRating,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            if (model.GenreIds != null && model.GenreIds.Any())
            {
                var genres = await _context.Genres.Where(g => model.GenreIds.Contains(g.GenreId)).ToListAsync();
                movie.Genres = genres;
            }

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm phim mới thành công.", movieId = movie.MovieId });
        }

        /// <summary>
        /// Cập nhật thông tin phim (Admin)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMovie(int id, [FromBody] CreateMovieDto model)
        {
            var movie = await _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            movie.Title = model.Title;
            movie.Description = model.Description;
            movie.Duration = model.Duration;
            movie.TrailerUrl = model.TrailerUrl;
            movie.PosterUrl = model.PosterUrl;
            movie.ReleaseDate = model.ReleaseDate;
            movie.EndDate = model.EndDate;
            movie.Language = model.Language;
            movie.Country = model.Country;
            movie.AgeRating = model.AgeRating;

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (movie.EndDate.HasValue && movie.EndDate.Value < today)
            {
                movie.IsActive = false;
            }

            if (model.GenreIds != null)
            {
                movie.Genres.Clear();
                var genres = await _context.Genres.Where(g => model.GenreIds.Contains(g.GenreId)).ToListAsync();
                movie.Genres = genres;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thông tin phim thành công." });
        }

        /// <summary>
        /// Bật / Tắt trạng thái phim (Admin)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> ToggleMovieStatus(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim." });

            movie.IsActive = !(movie.IsActive ?? true);
            await _context.SaveChangesAsync();

            return Ok(new { message = movie.IsActive == true ? "Đã bật hiển thị phim." : "Đã ẩn phim." });
        }
    }
}
