using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    [Route("Admin/[action]")]
    public class AdminTrackingController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminTrackingController(CinemaContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId"));
        }

        public async Task<IActionResult> CustomerActivityLogs(string activityType, int page = 1)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Admin");

            int pageSize = 50;

            var query = _context.UserActivityLogs
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Movie)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(activityType))
                query = query.Where(x => x.ActivityType == activityType);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.ActivityType = activityType;
            ViewBag.ActivityTypes = await _context.UserActivityLogs
                .Select(x => x.ActivityType)
                .Distinct()
                .ToListAsync();

            return View("~/Views/Admin/CustomerActivityLogs.cshtml", data);
        }

        public async Task<IActionResult> MovieViewStats()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Admin");

            var data = await _context.UserMovieViews
                .Include(v => v.Movie)
                .GroupBy(v => new { v.MovieId, v.Movie.Title, v.Movie.PosterUrl })
                .Select(g => new
                {
                    g.Key.MovieId,
                    g.Key.Title,
                    g.Key.PosterUrl,
                    TotalViews = g.Sum(x => x.ViewCount),
                    UniqueViewers = g.Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(30)
                .ToListAsync();

            return View("~/Views/Admin/MovieViewStats.cshtml", data);
        }

        public async Task<IActionResult> CustomerSearchLogs(int page = 1)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Admin");

            int pageSize = 50;

            var query = _context.UserSearchLogs
                .AsNoTracking()
                .Include(x => x.Customer)
                .OrderByDescending(x => x.CreatedAt);

            var total = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View("~/Views/Admin/CustomerSearchLogs.cshtml", data);
        }

        public async Task<IActionResult> TopSearchKeywords()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Admin");

            var data = await _context.UserSearchLogs
                .GroupBy(s => s.Keyword.ToLower())
                .Select(g => new
                {
                    Keyword = g.Key,
                    SearchCount = g.Count(),
                    AvgResultCount = g.Average(x => x.ResultCount ?? 0)
                })
                .OrderByDescending(x => x.SearchCount)
                .Take(30)
                .ToListAsync();

            return View("~/Views/Admin/TopSearchKeywords.cshtml", data);
        }

        public async Task<IActionResult> CustomerRecommendPreview(int customerId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Admin");

            var genreScores = await _context.Genres
                .Select(g => new
                {
                    g.GenreId,
                    g.Name,
                    Score =
                        _context.Tickets.Count(t =>
                            t.Showtime.Movie.Genres.Any(mg => mg.GenreId == g.GenreId) &&
                            t.Order!.CustomerId == customerId) * 3
                        +
                        (_context.UserMovieViews
                            .Where(v => v.CustomerId == customerId &&
                                        v.Movie.Genres.Any(mg => mg.GenreId == g.GenreId))
                            .Sum(v => (int?)v.ViewCount) ?? 0)
                })
                .OrderByDescending(x => x.Score)
                .ToListAsync();

            var topGenreIds = genreScores.Take(3).Select(x => x.GenreId).ToList();

            var recommended = await _context.Movies
                .Where(m => m.IsActive == true &&
                            m.Genres.Any(g => topGenreIds.Contains(g.GenreId)))
                .OrderByDescending(m => m.ReleaseDate)
                .Take(10)
                .ToListAsync();

            ViewBag.CustomerId = customerId;
            ViewBag.GenreScores = genreScores;
            ViewBag.Customer = await _context.Customers.FindAsync(customerId);

            return View("~/Views/Admin/CustomerRecommendPreview.cshtml", recommended);
        }
    }
}
