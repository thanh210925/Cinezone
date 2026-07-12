using CINEMA.Models;
using CINEMA.ViewModels;
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
            // Tự động chuyển các suất đã qua giờ chiếu thành đã ngưng
            var now = DateTime.Now;
            var expiredShowtimes = _context.Showtimes
                .Where(s => s.IsActive == true && s.StartTime < now)
                .ToList();
            if (expiredShowtimes.Any())
            {
                foreach (var s in expiredShowtimes)
                {
                    s.IsActive = false;
                }
                _context.SaveChanges();
            }

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
            ViewBag.Auditoriums = _context.Auditoriums.Include(a => a.Theater).ToList();
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
            ViewBag.Auditoriums = _context.Auditoriums.Include(a => a.Theater).ToList();

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
            var dbAuditoriums = _context.Auditoriums.Include(a => a.Theater).ToList();

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
                string roomName = room != null ? $"{room.Name} ({room.Theater?.Name})" : $"Phòng #{s1.AuditoriumId}";

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

        // ========================
        // 🔥 LƯU HÀNG LOẠT QUA AJAX JSON (Wizard V3)
        // ========================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult CreateMultipleJson([FromBody] List<ShowtimeJsonDto> items)
        {
            var errors = new List<string>();
            var toSave = new List<Showtime>();

            if (items == null || items.Count == 0)
                return Json(new { success = false, errors = new[] { "Danh sach rong!" } });

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // Parse datetime từ ISO string
                if (!DateTime.TryParse(item.StartTime, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var startDt))
                {
                    errors.Add($"Suat {i + 1}: Gio bat dau khong hop le ({item.StartTime})");
                    continue;
                }
                if (!DateTime.TryParse(item.EndTime, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var endDt))
                {
                    errors.Add($"Suat {i + 1}: Gio ket thuc khong hop le ({item.EndTime})");
                    continue;
                }
                if (startDt >= endDt)
                {
                    errors.Add($"Suat {i + 1}: Gio ket thuc phai sau gio bat dau ({item.StartTime} → {item.EndTime})");
                    continue;
                }

                // Kiểm tra trùng với DB
                var overlap = _context.Showtimes.FirstOrDefault(s =>
                    s.AuditoriumId == item.AuditoriumId && s.IsActive == true
                    && startDt < s.EndTime && endDt > s.StartTime);
                if (overlap != null)
                {
                    var movie = _context.Movies.Find(overlap.MovieId);
                    errors.Add($"Suat {i + 1} ({startDt:HH:mm}–{endDt:HH:mm}): Trung lich voi '{movie?.Title}' ({overlap.StartTime:HH:mm}–{overlap.EndTime:HH:mm})");
                    continue;
                }

                // Kiểm tra trùng trong chính batch
                bool dupInBatch = toSave.Any(s =>
                    s.AuditoriumId == item.AuditoriumId && startDt < s.EndTime && endDt > s.StartTime);
                if (dupInBatch) { errors.Add($"Suat {i + 1}: Trung gio voi mot suat khac trong danh sach."); continue; }

                toSave.Add(new Showtime
                {
                    MovieId = item.MovieId,
                    AuditoriumId = item.AuditoriumId,
                    StartTime = startDt,
                    EndTime = endDt,
                    BasePrice = (decimal?)item.BasePrice,
                    Language = item.Language,
                    IsActive = true
                });
            }

            if (toSave.Count == 0)
                return Json(new { success = false, errors });

            _context.Showtimes.AddRange(toSave);
            _context.SaveChanges();

            LogHelper.Write(_context, _httpContextAccessor, "ADDED_MULTIPLE_V3", "Showtime", toSave.Count);

            return Json(new { success = true, count = toSave.Count, skipped = errors.Count, errors });
        }


        // ========================
        // 🔥 KIỂM TRA TRÜNG LỊCH (AJAX) — dùng bởi wizard V3
        // ========================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult CheckConflicts([FromBody] List<ConflictCheckRequest> requests)
        {
            var results = new List<ConflictCheckResult>();
            if (requests == null) return Json(results);

            foreach (var req in requests)
            {
                var overlap = _context.Showtimes
                    .Include(s => s.Movie)
                    .FirstOrDefault(s => s.AuditoriumId == req.AuditoriumId
                                      && s.IsActive == true
                                      && req.StartTime < s.EndTime
                                      && req.EndTime > s.StartTime);
                results.Add(new ConflictCheckResult
                {
                    Index = req.Index,
                    HasConflict = overlap != null,
                    ConflictWith = overlap != null
                        ? $"{overlap.Movie?.Title} ({overlap.StartTime:HH:mm}–{overlap.EndTime:HH:mm})"
                        : null
                });
            }
            return Json(results);
        }

        // ========================
        // 🔥 LẤY LỊCH THEO NGÀY (AJAX) — dùng cho tính năng Copy & Auto-avoid
        // ========================
        [HttpGet]
        public IActionResult GetScheduleByDate(string date)
        {
            if (!DateOnly.TryParse(date, out var d))
                return Json(new List<object>());

            var startOfDay = d.ToDateTime(TimeOnly.MinValue);
            var endOfDay = d.ToDateTime(TimeOnly.MaxValue);

            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .Where(s => s.StartTime >= startOfDay && s.StartTime <= endOfDay && s.IsActive == true)
                .Select(s => new
                {
                    movieId = s.MovieId,
                    movieName = s.Movie != null ? s.Movie.Title : "",
                    auditoriumId = s.AuditoriumId,
                    auditoriumName = s.Auditorium != null ? s.Auditorium.Name : "",
                    startTime = s.StartTime != null ? s.StartTime.Value.ToString("HH:mm") : "",
                    endTime = s.EndTime != null ? s.EndTime.Value.ToString("HH:mm") : "",
                    basePrice = s.BasePrice,
                    language = s.Language
                })
                .ToList();

            return Json(showtimes);
        }
    }

}
