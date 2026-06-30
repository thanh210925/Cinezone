using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CINEMA.Controllers
{
    public class PositionController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public PositionController(CinemaContext context)
        {
            _context = context;
        }

        // Danh sách
        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Positions.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.PositionName.Contains(search));
            }

            return View(await query.ToListAsync());
        }

        // GET Create
        public IActionResult Create()
        {
            return View();
        }

        // POST Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Position position)
        {
            if (ModelState.IsValid)
            {
                position.IsActive = true;

                _context.Positions.Add(position);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(position);
        }

        // GET Edit
        public async Task<IActionResult> Edit(int id)
        {
            var position = await _context.Positions.FindAsync(id);

            if (position == null)
                return NotFound();

            return View(position);
        }

        // POST Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Position position)
        {
            if (id != position.PositionId)
                return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(position);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(position);
        }

        // Xóa hẳn (Hard Delete)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var position = await _context.Positions.FindAsync(id);

            if (position != null)
            {
                _context.Positions.Remove(position); // Lệnh xóa hoàn toàn khỏi DB
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}