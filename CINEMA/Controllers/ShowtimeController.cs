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
            ViewBag.Movies = _context.Movies.Where(m => m.IsActive == true).ToList();
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

            ViewBag.Movies = _context.Movies.ToList();
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
            if (showtimes != null && showtimes.Any())
            {
                _context.Showtimes.AddRange(showtimes);
                _context.SaveChanges();
            }
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
