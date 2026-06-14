using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Controllers
{
    public class LogController : Controller
    {
        private readonly CinemaContext _context;

        public LogController(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            // 1. Kiểm tra đăng nhập bằng AdminId (đảm bảo đồng bộ với Login controller)
            var adminId = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminId))
            {
                return RedirectToAction("Login", "Admin");
            }

            // 2. Cấu hình phân trang
            int pageSize = 20; // Số dòng trên mỗi trang
            var query = _context.ActivityLogs.Include(l => l.Admin).OrderByDescending(l => l.LogDate);

            int totalLogs = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            // 3. Lấy dữ liệu theo trang
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Truyền thông tin sang View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(logs);
        }
    }
}