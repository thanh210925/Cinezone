using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;

namespace CINEMA.Controllers
{
    public class AdminController : Controller
    {
        private readonly CinemaContext _context;

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
                HttpContext.Session.SetString("Role", "Admin");
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
            if (HttpContext.Session.GetString("Role") != "Admin")
            {
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.Name = HttpContext.Session.GetString("Name");

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
            if (HttpContext.Session.GetString("Role") != "Admin") return RedirectToAction("Login");
            return View(_context.Admins.ToList());
        }

        public IActionResult CreateStaff() => View();

        [HttpPost]
        public IActionResult CreateStaff(Admin admin)
        {
            admin.CreatedAt = DateTime.Now;
            _context.Admins.Add(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(StaffList));
        }

        public IActionResult EditStaff(int id)
        {
            var admin = _context.Admins.Find(id);
            return admin == null ? NotFound() : View(admin);
        }

        [HttpPost]
        public IActionResult EditStaff(Admin admin)
        {
            _context.Admins.Update(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(StaffList));
        }

        [HttpPost]
        public IActionResult DeleteStaff(int id)
        {
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
    }
}

