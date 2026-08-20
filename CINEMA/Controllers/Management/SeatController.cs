using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class SeatController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public SeatController(CinemaContext context)
        {
            _context = context;
        }

        // 🟢 Danh sách ghế
        public IActionResult Index()
        {
            var seats = _context.Seats
                .Include(s => s.Auditorium)
                .OrderBy(s => s.AuditoriumId)
                .ThenBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();
            return View(seats);
        }

        // 🟢 Thêm mới
        public IActionResult Create()
        {
            ViewBag.Auditoria = _context.Auditoriums.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Seat seat)
        {
            if (ModelState.IsValid)
            {
                _context.Seats.Add(seat);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Auditoria = _context.Auditoriums.ToList();
            return View(seat);
        }

        // 🟢 Sửa
        public IActionResult Edit(int id)
        {
            var seat = _context.Seats.Find(id);
            if (seat == null) return NotFound();

            ViewBag.Auditoria = _context.Auditoriums.ToList();
            return View(seat);
        }

        [HttpPost]
        public IActionResult Edit(Seat seat)
        {
            if (ModelState.IsValid)
            {
                _context.Seats.Update(seat);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Auditoria = _context.Auditoriums.ToList();
            return View(seat);
        }

        // 🟢 Xóa
        public IActionResult Delete(int id)
        {
            var seat = _context.Seats
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.SeatId == id);
            if (seat == null) return NotFound();
            return View(seat);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var seat = _context.Seats.Find(id);
            if (seat != null)
            {
                _context.Seats.Remove(seat);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
