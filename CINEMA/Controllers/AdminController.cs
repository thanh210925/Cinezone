using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;

namespace CINEMA.Controllers
{
    public class AdminController : Controller
    {
        private readonly CinemaContext _context;
        private bool IsSuperAdmin()
        {
            return HttpContext.Session.GetString("Role") == "SuperAdmin";
        }
        private bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId"));
        }
        public AdminController(CinemaContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string fullName, string email, string password, string? phone)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            var existAdmin = _context.Admins.FirstOrDefault(a => a.Email == email);
            if (existAdmin != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View();
            }

            var admin = new Admin
            {
                FullName = fullName,
                Email = email,
                PasswordHash = password,
                Phone = phone,
                Role = "Staff",
                CreatedAt = DateTime.Now
            };

            _context.Admins.Add(admin);
            _context.SaveChanges();

            return RedirectToAction("Login", "Admin");
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email && a.PasswordHash == password);
            if (admin != null)
            {
                HttpContext.Session.SetString("AdminId", admin.AdminId.ToString());
                HttpContext.Session.SetString("Role", admin.Role ?? "Staff");
                HttpContext.Session.SetString("Name", admin.FullName);

                admin.LastLogin = DateTime.Now;
                _context.SaveChanges();

                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }
        public IActionResult Dashboard()
        {
            // Kiểm tra xem đã có AdminId trong session hay chưa
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId")))
            {
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.Name = HttpContext.Session.GetString("Name");
            // Lấy Role để view có thể hiển thị thông tin hoặc ẩn hiện menu
            ViewBag.Role = HttpContext.Session.GetString("Role");

            ViewBag.TotalMovies = _context.Movies.Count();
            ViewBag.TotalCustomers = _context.Customers.Count();
            ViewBag.TotalOrders = _context.Orders.Count();

            ViewBag.TopMovies = _context.Tickets
                .GroupBy(t => t.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieName = g.Key,
                    TotalTickets = g.Count()
                })
                .OrderByDescending(x => x.TotalTickets)
                .Take(5)
                .ToList();

            return View();
        }
        public IActionResult StaffList()
        {
            // Chỉ SuperAdmin mới được xem danh sách
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này!";
                return RedirectToAction("Dashboard");
            }
            return View(_context.Admins.ToList());
        }

        public IActionResult CreateStaff() => View();

        [HttpPost]
        public IActionResult CreateStaff(Admin admin)
        {
            // Chỉ SuperAdmin mới được tạo nhân viên
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("Dashboard");
            }

            admin.CreatedAt = DateTime.Now;
            admin.Role = "Staff"; // Mặc định tạo mới là Staff+
            _context.Admins.Add(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(StaffList));
        }

        public IActionResult EditStaff(int id)
        {
            // Chỉ SuperAdmin mới được chỉnh sửa nhân viên
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("Dashboard");
            }

            var admin = _context.Admins.Find(id);
            return admin == null ? NotFound() : View(admin);
        }

        [HttpPost]
        public IActionResult EditStaff(Admin admin)
        {
            // Chỉ SuperAdmin mới được chỉnh sửa nhân viên
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("Dashboard");
            }
admin.CreatedAt = DateTime.Now;
            _context.Admins.Update(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(StaffList));
        }

        [HttpPost]
        public IActionResult DeleteStaff(int id)
        {
            // Chỉ SuperAdmin mới được xóa nhân viên
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("Dashboard");
            }

            var admin = _context.Admins.Find(id);
            if (admin != null)
            {
                _context.Admins.Remove(admin);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(StaffList));
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Admin");
        }
        public IActionResult DetailsStaff(int id)
        {
            var admin = _context.Admins.Find(id);
            if (admin == null) return NotFound();
            return View(admin);
        }
    }
}

