using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class TheaterController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public TheaterController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 Hiển thị danh sách rạp chiếu
        public IActionResult Index()
        {
            var theaters = _context.Theaters
                .OrderBy(t => t.TheaterId)
                .ToList();
            return View(theaters);
        }

        // ➕ Trang thêm mới
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Theater theater)
        {
            if (!ModelState.IsValid)
                return View(theater);

            theater.CreatedAt = DateTime.Now;
            theater.IsActive = true;

            _context.Theaters.Add(theater);
            _context.SaveChanges();

            // Tạo Branch tương ứng
            Branch branch = new Branch
            {
                TheaterId = theater.TheaterId,
                BranchCode = $"CN{theater.TheaterId:D3}",
                BranchName = theater.Name,
                Address = theater.Address,
                Phone = theater.Phone,
                Email = "",
                IsActive = theater.IsActive ?? true,
                CreatedAt = DateTime.Now
            };

            _context.Branches.Add(branch);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // ✏️ Trang chỉnh sửa
        public IActionResult Edit(int id)
        {
            var theater = _context.Theaters.Find(id);
            if (theater == null)
                return NotFound();

            return View(theater);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Theater theater)
        {
            if (!ModelState.IsValid)
                return View(theater);

            _context.Theaters.Update(theater);

            var branch = _context.Branches
                .FirstOrDefault(b => b.TheaterId == theater.TheaterId);

            if (branch != null)
            {
                branch.BranchName = theater.Name;
                branch.Address = theater.Address;
                branch.Phone = theater.Phone;
                branch.IsActive = theater.IsActive ?? true;

                _context.Branches.Update(branch);
            }

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // 🗑️ Trang xác nhận xóa
        public IActionResult Delete(int id)
        {
            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == id);
            if (theater == null)
                return NotFound();

            return View(theater);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var theater = _context.Theaters.Find(id);

            if (theater == null)
                return RedirectToAction(nameof(Index));

            theater.IsActive = false;

            var branch = _context.Branches
                .FirstOrDefault(b => b.TheaterId == id);

            if (branch != null)
            {
                branch.IsActive = false;
            }

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}
