using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class AdminController : Controller
    {
        private readonly CinemaContext _context;

        public AdminController(CinemaContext context)
        {
            _context = context;
        }

        private string GenerateEmployeeCode()
        {
            var lastCode = _context.Admins
                .Where(x => x.EmployeeCode != null)
                .OrderByDescending(x => x.AdminId)
                .Select(x => x.EmployeeCode)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith("NV"))
                return "NV001";

            var numberPart = lastCode.Substring(2);
            if (!int.TryParse(numberPart, out int number))
                return "NV001";

            number++;
            return $"NV{number:D3}";
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            return View("~/Views/Admin/Register.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string email, string password, string? phone)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View("~/Views/Admin/Register.cshtml");
            }

            var existAdmin = _context.Admins.AsNoTracking().FirstOrDefault(a => a.Email == email);
            if (existAdmin != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View("~/Views/Admin/Register.cshtml");
            }

            var admin = new Admin
            {
                FullName = fullName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Phone = phone,
                Role = "Staff",
                CreatedAt = DateTime.Now
            };

            _context.Admins.Add(admin);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Admin");
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            return View("~/Views/Admin/Login.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email);
            if (admin != null)
            {
                bool checkPassword = false;
                try
                {
                    checkPassword = BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
                }
                catch
                {
                    checkPassword = (admin.PasswordHash == password);
                    if (checkPassword)
                    {
                        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                        await _context.SaveChangesAsync();
                    }
                }

                if (checkPassword)
                {
                    if (!admin.IsActive)
                    {
                        ViewBag.Error = "Tài khoản đã bị khóa";
                        return View("~/Views/Admin/Login.cshtml");
                    }

                    HttpContext.Session.SetString("AdminId", admin.AdminId.ToString());
                    HttpContext.Session.SetString("Role", admin.Role ?? "Staff");
                    HttpContext.Session.SetString("Name", admin.FullName);

                    if (string.IsNullOrEmpty(admin.EmployeeCode))
                    {
                        admin.EmployeeCode = GenerateEmployeeCode();
                        await _context.SaveChangesAsync();
                    }

                    admin.LastLogin = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View("~/Views/Admin/Login.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Admin");
        }

        public async Task<IActionResult> Dashboard()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId")))
            {
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.Name = HttpContext.Session.GetString("Name");
            ViewBag.Role = HttpContext.Session.GetString("Role");

            ViewBag.TotalMovies = _context.Movies.Count();
            ViewBag.TotalCustomers = _context.Customers.Count();
            ViewBag.TotalOrders = _context.Orders.Count();

            int totalReviews = 0;
            int totalReported = 0;
            try
            {
                totalReviews = _context.Reviews.Count();
                totalReported = _context.Reviews.Count(r => r.HasReport == true);
            }
            catch { }
            ViewBag.TotalReviews = totalReviews;
            ViewBag.TotalReportedReviews = totalReported;

            ViewBag.TopMovies = _context.Tickets
                .Include(t => t.Showtime)
                    .ThenInclude(s => s!.Movie)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null)
                .GroupBy(t => t.Showtime!.Movie!.Title)
                .Select(g => new
                {
                    MovieName = g.Key,
                    TotalTickets = g.Count()
                })
                .OrderByDescending(x => x.TotalTickets)
                .Take(5)
                .ToList();

            return View("~/Views/Admin/Dashboard.cshtml");
        }
    }
}
