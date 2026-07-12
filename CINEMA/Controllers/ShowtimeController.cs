using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Helpers;
namespace CINEMA.Controllers
{
    public class ShowtimeController : AdminBaseController
    {
        private readonly CinemaContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ShowtimeController(CinemaContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;

        }

        // ========================
        // 🔥 DANH SÁCH LỊCH CHIẾU
        // ========================
        public IActionResult Index()
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .OrderByDescending(s => s.StartTime)
                .ToList();

            ViewBag.ActiveShowtimes = showtimes.Where(s => s.IsActive == true).ToList();
            ViewBag.EndedShowtimes = showtimes.Where(s => s.IsActive == false).ToList();

            return View();
        }

        // ========================
        // 🔥 TẠO LỊCH CHIẾU
        // ========================
        public IActionResult Create()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            ViewBag.Movies = _context.Movies
                .Where(m => m.IsActive == true
                         && m.ReleaseDate <= today)
                .OrderBy(m => m.Title)
                .ToList();
            ViewBag.Auditoriums = _context.Auditoriums.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Showtime showtime)
        {
            if (!ModelState.IsValid)
                return View(showtime);

            showtime.IsActive = true;

            _context.Showtimes.Add(showtime);
            _context.SaveChanges();
            LogHelper.Write(
    _context,
    _httpContextAccessor,
    "ADDED",
    "Showtime",
    showtime.ShowtimeId
);
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // 🔥 SỬA
        // ========================
        public IActionResult Edit(int id)
        {
            var showtime = _context.Showtimes.Find(id);
            if (showtime == null) return NotFound();

            ViewBag.Movies = _context.Movies
      .Where(m => m.IsActive == true)
      .OrderBy(m => m.Title)
      .ToList();
            ViewBag.Auditoriums = _context.Auditoriums.ToList();

            return View(showtime);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Showtime showtime)
        {
            if (!ModelState.IsValid)
                return View(showtime);

            _context.Showtimes.Update(showtime);
            _context.SaveChanges();
            LogHelper.Write(
    _context,
    _httpContextAccessor,
    "MODIFIED",
    "Showtime",
    showtime.ShowtimeId
);
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // 🔥 XÓA LỊCH CHIẾU
        // ========================
        public IActionResult Delete(int id)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound();
            return View(showtime);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            // 1. Lấy tất cả tickets của suất chiếu
            var tickets = _context.Tickets
                .Where(t => t.ShowtimeId == id)
                .Include(t => t.TicketCombos)
                .ToList();

            // 2. Xóa ticket combos trước
            foreach (var t in tickets)
            {
                if (t.TicketCombos != null && t.TicketCombos.Any())
                {
                    _context.TicketCombos.RemoveRange(t.TicketCombos);
                }
            }
            _context.SaveChanges();

            // 3. Xoá tickets
            if (tickets.Any())
            {
                _context.Tickets.RemoveRange(tickets);
                _context.SaveChanges();
            }

            // 4. Xóa suất chiếu
            var showtime = _context.Showtimes.Find(id);
            if (showtime != null)
            {
                int showtimeId = showtime.ShowtimeId;

                _context.Showtimes.Remove(showtime);
                _context.SaveChanges();

                LogHelper.Write(
                    _context,
                    _httpContextAccessor,
                    "DELETED",
                    "Showtime",
                    showtimeId
                );
            }
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // 🔥 BẬT LẠI SUẤT CHIẾU
        // ========================
        [HttpPost]
        public IActionResult Activate(int id)
        {
            var showtime = _context.Showtimes.Find(id);
            if (showtime == null) return NotFound();

            showtime.IsActive = true;
            _context.SaveChanges();
            LogHelper.Write(
     _context,
     _httpContextAccessor,
     "ACTIVATED",
     "Showtime",
     showtime.ShowtimeId
 );
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> Deactivate(int id)
        {
            var show = await _context.Showtimes.FindAsync(id);
            if (show == null) return NotFound();

            show.IsActive = false;
            await _context.SaveChangesAsync();
            LogHelper.Write(
     _context,
     _httpContextAccessor,
     "DEACTIVATED",
     "Showtime",
     show.ShowtimeId
 );
            return RedirectToAction("Index");
        }
        [HttpPost]
        public IActionResult CreateMultiple(List<Showtime> showtimes)
        {
            if (showtimes == null || !showtimes.Any())
            {
                return RedirectToAction("Index");
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var dbMovies = _context.Movies.ToList();
            var dbAuditoriums = _context.Auditoriums.ToList();

            // 1. Kiểm tra tính hợp lệ của từng dòng và kiểm tra trùng lặp lịch
            for (int i = 0; i < showtimes.Count; i++)
            {
                var s1 = showtimes[i];
                if (s1.MovieId == null || s1.AuditoriumId == null || s1.StartTime == null || s1.EndTime == null)
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng nhập đầy đủ thông tin.");
                    continue;
                }

                if (s1.StartTime >= s1.EndTime)
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Thời gian bắt đầu phải trước thời gian kết thúc.");
                    continue;
                }

                var movie = dbMovies.FirstOrDefault(m => m.MovieId == s1.MovieId);
                var room = dbAuditoriums.FirstOrDefault(a => a.AuditoriumId == s1.AuditoriumId);
                string movieTitle = movie?.Title ?? $"Phim #{s1.MovieId}";
                string roomName = room?.Name ?? $"Phòng #{s1.AuditoriumId}";

                // Kiểm tra trùng lặp trong chính danh sách gửi lên
                for (int j = i + 1; j < showtimes.Count; j++)
                {
                    var s2 = showtimes[j];
                    if (s1.AuditoriumId == s2.AuditoriumId && s1.StartTime < s2.EndTime && s1.EndTime > s2.StartTime)
                    {
                        ModelState.AddModelError("", $"Dòng {i + 1} bị trùng thời gian chiếu với Dòng {j + 1} tại phòng {roomName}.");
                    }
                }

                // Kiểm tra trùng lặp với CSDL (Suất chiếu đang hoạt động và giao nhau)
                var overlap = _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Auditorium)
                    .FirstOrDefault(s => s.AuditoriumId == s1.AuditoriumId 
                                      && s.IsActive == true 
                                      && s1.StartTime < s.EndTime 
                                      && s1.EndTime > s.StartTime);

                if (overlap != null)
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Suất chiếu của phim '{movieTitle}' ({s1.StartTime:dd/MM/yyyy HH:mm} - {s1.EndTime:HH:mm}) bị trùng lịch với phim '{overlap.Movie?.Title}' ({overlap.StartTime:dd/MM/yyyy HH:mm} - {overlap.EndTime:HH:mm}) đang xếp tại phòng {roomName}.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Movies = _context.Movies
                    .Where(m => m.IsActive == true && m.ReleaseDate <= today)
                    .OrderBy(m => m.Title)
                    .ToList();
                ViewBag.Auditoriums = dbAuditoriums;
                return View("Create", showtimes);
            }

            // Gán trạng thái hoạt động và lưu
            foreach (var showtime in showtimes)
            {
                showtime.IsActive = true;
            }

            _context.Showtimes.AddRange(showtimes);
            _context.SaveChanges();

            LogHelper.Write(
                _context,
                _httpContextAccessor,
                "ADDED_MULTIPLE",
                "Showtime",
                showtimes.Count
            );

            return RedirectToAction("Index");
        }
    }

}
