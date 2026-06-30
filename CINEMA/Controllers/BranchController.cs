using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class BranchController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public BranchController(CinemaContext context)
        {
            _context = context;
        }

        // Danh sách
        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Branches.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.BranchName.Contains(search) ||
                    x.BranchCode.Contains(search));
            }

            return View(await query
                .OrderBy(x => x.BranchId)
                .ToListAsync());
        }

        // GET
        public IActionResult Create()
        {
            return View();
        }

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch)
        {
            if (ModelState.IsValid)
            {
                branch.CreatedAt = DateTime.Now;
                branch.IsActive = true;

                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        // GET
        public async Task<IActionResult> Edit(int id)
        {
            var branch = await _context.Branches.FindAsync(id);

            if (branch == null)
                return NotFound();

            return View(branch);
        }

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch branch)
        {
            if (id != branch.BranchId)
                return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(branch);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        // Xóa mềm
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);

            if (branch != null)
            {
                branch.IsActive = false;

                _context.Update(branch);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}