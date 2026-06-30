using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class ShiftController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public ShiftController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Shift
        public async Task<IActionResult> Index()
        {
            return View(await _context.Shifts.ToListAsync());
        }

        // GET: Shift/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var shift = await _context.Shifts.FirstOrDefaultAsync(m => m.ShiftId == id);
            if (shift == null) return NotFound();

            return View(shift);
        }

        // GET: Shift/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Shift/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ShiftId,ShiftName,StartTime,EndTime")] Shift shift)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(shift);
        }

        // GET: Shift/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null) return NotFound();

            return View(shift);
        }

        // POST: Shift/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ShiftId,ShiftName,StartTime,EndTime")] Shift shift)
        {
            if (id != shift.ShiftId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shift);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShiftExists(shift.ShiftId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(shift);
        }

        // GET: Shift/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var shift = await _context.Shifts.FirstOrDefaultAsync(m => m.ShiftId == id);
            if (shift == null) return NotFound();

            return View(shift);
        }

        // POST: Shift/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null)
            {
                _context.Shifts.Remove(shift); // Xóa cứng khỏi Database
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ShiftExists(int id)
        {
            return _context.Shifts.Any(e => e.ShiftId == id);
        }
    }
}