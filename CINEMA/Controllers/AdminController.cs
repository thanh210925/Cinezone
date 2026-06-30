using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> Register()
        {
            return View();
        }

        private string GenerateEmployeeCode()
        {
            var lastCode = _context.Admins
        .Where(x => x.EmployeeCode != null)
        .OrderByDescending(x => x.AdminId)
        .Select(x => x.EmployeeCode)
        .FirstOrDefault();

            if (string.IsNullOrEmpty(lastCode))
                return "NV001";

            if (!lastCode.StartsWith("NV"))
                return "NV001";

            var numberPart = lastCode.Substring(2);

            if (!int.TryParse(numberPart, out int number))
                return "NV001";

            number++;

            return $"NV{number:D3}";
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string email, string password, string? phone)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            var existAdmin = _context.Admins.AsNoTracking().FirstOrDefault(a => a.Email == email);
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
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", "Admin");
        }
        [HttpGet]
        public async Task<IActionResult> Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email && a.PasswordHash == password);
            if (admin != null)
            {
                HttpContext.Session.SetString("AdminId", admin.AdminId.ToString());
                HttpContext.Session.SetString("Role", admin.Role ?? "Staff");
                HttpContext.Session.SetString("Name", admin.FullName);

                if (!admin.IsActive)
                {
                    ViewBag.Error =
                        "Tài khoản đã bị khóa";
                    return View();
                }

                if (string.IsNullOrEmpty(admin.EmployeeCode))
                {
                    admin.EmployeeCode = GenerateEmployeeCode();
                    await _context.SaveChangesAsync();
                }

                admin.LastLogin = DateTime.Now;
                await _context.SaveChangesAsync();

                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";

            return View();
        }
        public async Task<IActionResult> Dashboard()
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
        public async Task<IActionResult> StaffList(string search)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Dashboard");
            }

            var query = _context.Admins
                .Include(x => x.Branch)
                .Include(x => x.Position)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.FullName.Contains(search) ||
                    x.Email.Contains(search));
            }

            return View(query.ToList());
        }

        public async Task<IActionResult> CreateStaff()
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Branches = new SelectList(
        _context.Branches.ToList(),
        "BranchId",
        "BranchName");

            ViewBag.Positions = new SelectList(
                _context.Positions.ToList(),
                "PositionId",
                "PositionName");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateStaff(
    Admin admin,
    IFormFile avatar)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            if (ModelState.IsValid)
            {

                // AUTO CODE
                admin.EmployeeCode = GenerateEmployeeCode();
                // upload avatar
                if (avatar != null)
                {
                    string folder =
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot/uploads/admin");

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string fileName =
                        Guid.NewGuid().ToString() +
                        Path.GetExtension(
                            avatar.FileName);

                    string path =
                        Path.Combine(
                            folder,
                            fileName);

                    using (var stream =
                        new FileStream(
                            path,
                            FileMode.Create))
                    {
                        await avatar.CopyToAsync(stream);
                    }

                    admin.Avatar =
                        "/uploads/admin/" +
                        fileName;
                }

                admin.CreatedAt = DateTime.Now;

                if (string.IsNullOrEmpty(admin.Role))
                    admin.Role = "Staff";

                admin.IsActive = true;

                _context.Admins.Add(admin);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(StaffList));
            }

            ViewBag.Branches =
                new SelectList(
                    _context.Branches,
                    "BranchId",
                    "BranchName");

            ViewBag.Positions =
                new SelectList(
                    _context.Positions,
                    "PositionId",
                    "PositionName");

            return View(admin);
        }
        
        public async Task<IActionResult> EditStaff(int id)
        {
            // Chỉ SuperAdmin mới được chỉnh sửa nhân viên
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            var admin = _context.Admins.Find(id);

            if (admin == null)
                return NotFound();

            ViewBag.Branches =
                new SelectList(
                    _context.Branches,
                    "BranchId",
                    "BranchName",
                    admin.BranchId);

            ViewBag.Positions =
                new SelectList(
                    _context.Positions,
                    "PositionId",
                    "PositionName",
                    admin.PositionId);

            return View(admin);
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(
    Admin admin,
    IFormFile avatar)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            var oldAdmin =
                await _context.Admins
                    .FirstOrDefaultAsync(
                        x => x.AdminId == admin.AdminId);

            if (oldAdmin == null)
                return NotFound();


            oldAdmin.FullName = admin.FullName;
            oldAdmin.Email = admin.Email;
            oldAdmin.Phone = admin.Phone;
            oldAdmin.Role = admin.Role;
            oldAdmin.BranchId = admin.BranchId;
            oldAdmin.PositionId = admin.PositionId;
            oldAdmin.Address = admin.Address;
            oldAdmin.BirthDate = admin.BirthDate;
            oldAdmin.IsActive = admin.IsActive;
            oldAdmin.CitizenId = admin.CitizenId;
            oldAdmin.CreatedAt = admin.CreatedAt;
            oldAdmin.Gender = admin.Gender;
            oldAdmin.JobInfo = admin.JobInfo;
            oldAdmin.HireDate = admin.HireDate;


            if (avatar != null)
            {
                string folder =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot/uploads/admin");

                string fileName =
                    Guid.NewGuid() +
                    Path.GetExtension(
                        avatar.FileName);

                string path =
                    Path.Combine(
                        folder,
                        fileName);

                using (var stream =
                    new FileStream(
                        path,
                        FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                oldAdmin.Avatar =
                    "/uploads/admin/" +
                    fileName;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(StaffList));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(int id)
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
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(StaffList));
        }
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Admin");
        }
        public async Task<IActionResult> DetailsStaff(int id)
        {
            var admin = _context.Admins
        .Include(x => x.Branch)
        .Include(x => x.Position)
        .FirstOrDefault(x => x.AdminId == id);

            if (admin == null)
                return NotFound();

            return View(admin);
        }
        [HttpPost]
        public async Task<IActionResult> ToggleStaffStatus(int id)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] =
                    "Bạn không có quyền!";

                return RedirectToAction("Dashboard");
            }

            var admin =
                _context.Admins
                    .FirstOrDefault(
                        x => x.AdminId == id);

            if (admin == null)
                return NotFound();

            // không cho khóa chính mình
            var currentAdmin =
                HttpContext.Session
                    .GetString("AdminId");

            if (currentAdmin ==
                admin.AdminId.ToString())
            {
                TempData["Error"] =
                    "Không thể khóa chính mình.";

                return RedirectToAction(nameof(StaffList));
            }

            admin.IsActive =
                !admin.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                admin.IsActive
                ? "Đã mở khóa tài khoản."
                : "Đã khóa tài khoản.";

            return RedirectToAction(nameof(StaffList));
        }
        public async Task<IActionResult> ResetPassword(int id)
        {
            if (!IsSuperAdmin())
                return RedirectToAction("Dashboard");

            var admin =
                _context.Admins.Find(id);

            if (admin == null)
                return NotFound();

            return View(admin);
        }
        [HttpPost]
        public async Task<IActionResult> ResetPassword(
    int id,
    string newPassword)
        {
            if (!IsSuperAdmin())
                return RedirectToAction("Dashboard");

            var admin =
                _context.Admins.Find(id);

            if (admin == null)
                return NotFound();

            admin.PasswordHash =
                newPassword;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Đặt lại mật khẩu thành công";

            return RedirectToAction(nameof(StaffList));
        }
        public async Task<IActionResult> EmployeeDashboard()
        {
            if (!IsSuperAdmin())
                return RedirectToAction("Dashboard");

            var model = new EmployeeDashboardViewModel();

            model.TotalEmployees =
                _context.Admins.Count();

            model.ActiveEmployees =
                _context.Admins.Count(x => x.IsActive);

            model.LockedEmployees =
                _context.Admins.Count(x => !x.IsActive);

            model.TotalBranches =
                _context.Branches.Count();

            model.TotalPositions =
                _context.Positions.Count();

            model.BranchLabels =
                _context.Branches
                    .Select(x => x.BranchName)
                    .ToList();

            model.BranchValues =
                _context.Branches
                    .Select(x =>
                        _context.Admins.Count(a =>
                            a.BranchId == x.BranchId))
                    .ToList();

            model.PositionLabels =
                _context.Positions
                    .Select(x => x.PositionName)
                    .ToList();

            model.PositionValues =
                _context.Positions
                    .Select(x =>
                        _context.Admins.Count(a =>
                            a.PositionId == x.PositionId))
                    .ToList();

            model.RecentLogins =
                _context.Admins
                    .OrderByDescending(x => x.LastLogin)
                    .Take(10)
                    .Include(x => x.Branch)
                    .Include(x => x.Position)
                    .ToList();

            return View(model);
        }
        public async Task<IActionResult> ActivityLogs(string search, string action)
        {
            if (!IsSuperAdmin())
                return RedirectToAction("Dashboard");

            var query = _context.ActivityLogs
                .AsNoTracking()
                .Include(x => x.Admin)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Entity.Contains(search));

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(x => x.Action == action);

            var data = await query
                .OrderByDescending(x => x.LogDate)
                .ToListAsync();

            ViewBag.Count = data.Count;

            return View(data);
        }
    }
}

