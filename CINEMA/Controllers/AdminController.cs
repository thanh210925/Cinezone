using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Drawing; // Nếu cần xử lý ảnh

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

            if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith("NV"))
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
            return View();
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
                    // Cơ chế nâng cấp bảo mật: Nếu chưa băm bằng BCrypt, đối chiếu văn bản thuần
                    checkPassword = (admin.PasswordHash == password);
                    if (checkPassword)
                    {
                        // Tự động nâng cấp mật khẩu sang BCrypt
                        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                        await _context.SaveChangesAsync();
                    }
                }

                if (checkPassword)
                {
                    if (!admin.IsActive)
                    {
                        ViewBag.Error = "Tài khoản đã bị khóa";
                        return View();
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
            return View();
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
            ViewBag.TheaterDict = _context.Theaters.ToDictionary(t => t.TheaterId, t => t.Name);
            var query = _context.Admins
                .Include(x => x.Branch)
                    .ThenInclude(b => b.Theater)
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

            // Lấy trực tiếp từ bảng Theaters
            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name");
            ViewBag.Positions = new SelectList(_context.Positions.ToList(), "PositionId", "PositionName");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateStaff(Admin admin, IFormFile avatar)
        {
            // 1. Kiểm tra quyền
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            // 2. "Gỡ bỏ" các trường không cần người dùng nhập ở form
            // Hệ thống sẽ tự xử lý các trường này trong code bên dưới
            ModelState.Remove("Branch");
            ModelState.Remove("Position");
            ModelState.Remove("Theater");
            ModelState.Remove("EmployeeCode");
            ModelState.Remove("Avatar");
            // 3. Kiểm tra tính hợp lệ của Model
            if (ModelState.IsValid)
            {
                try
                {
                    // Tự động sinh mã nhân viên (NV001, NV002...)
                    admin.EmployeeCode = GenerateEmployeeCode();

                    // Mật khẩu mặc định nếu chưa nhập
                    if (string.IsNullOrEmpty(admin.PasswordHash))
                    {
                        admin.PasswordHash = "123456";
                    }
                    admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(admin.PasswordHash);

                    // Xử lý Avatar
                    if (avatar != null && avatar.Length > 0)
                    {
                        string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/admin");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatar.FileName);
                        string path = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            await avatar.CopyToAsync(stream);
                        }
                        admin.Avatar = "/uploads/admin/" + fileName;
                    }

                    // Gán các thông tin mặc định
                    admin.CreatedAt = DateTime.Now;
                    admin.IsActive = true;
                    if (string.IsNullOrEmpty(admin.Role)) admin.Role = "Staff";

                    // Lưu vào Database
                    _context.Admins.Add(admin);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Thêm nhân viên thành công!";
                    return RedirectToAction(nameof(StaffList));
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "Có lỗi xảy ra khi lưu: " + ex.Message;
                }
            }
            else
            {
                // Debug lỗi nếu Model không hợp lệ
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                ViewBag.Error = "Thông tin không hợp lệ. Vui lòng kiểm tra lại các trường!";
            }

            // 4. Nạp lại Dropdown nếu bị lỗi (để người dùng không phải chọn lại)
            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name", admin.BranchId);
            ViewBag.Positions = new SelectList(_context.Positions.ToList(), "PositionId", "PositionName", admin.PositionId);

            return View(admin);
        }

        public async Task<IActionResult> EditStaff(int id)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            var admin = _context.Admins.Find(id);
            if (admin == null) return NotFound();

            // Load rạp và gán giá trị đang có của NV
            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name", admin.BranchId);
            ViewBag.Positions = new SelectList(_context.Positions, "PositionId", "PositionName", admin.PositionId);

            return View(admin);
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(Admin admin, IFormFile avatar)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            var oldAdmin = await _context.Admins.FirstOrDefaultAsync(x => x.AdminId == admin.AdminId);
            if (oldAdmin == null) return NotFound();

            oldAdmin.FullName = admin.FullName;
            oldAdmin.Email = admin.Email;
            oldAdmin.Phone = admin.Phone;
            oldAdmin.Role = admin.Role;
            oldAdmin.BranchId = admin.BranchId; // Lưu mã Rạp
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
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/admin");
                string fileName = Guid.NewGuid() + Path.GetExtension(avatar.FileName);
                string path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                oldAdmin.Avatar = "/uploads/admin/" + fileName;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(StaffList));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(int id)
        {
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

            if (admin == null) return NotFound();
            return View(admin);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStaffStatus(int id)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard");
            }

            var admin = _context.Admins.FirstOrDefault(x => x.AdminId == id);
            if (admin == null) return NotFound();

            var currentAdmin = HttpContext.Session.GetString("AdminId");
            if (currentAdmin == admin.AdminId.ToString())
            {
                TempData["Error"] = "Không thể khóa chính mình.";
                return RedirectToAction(nameof(StaffList));
            }

            admin.IsActive = !admin.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = admin.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";

            return RedirectToAction(nameof(StaffList));
        }

        public async Task<IActionResult> ResetPassword(int id)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            var admin = _context.Admins.Find(id);
            if (admin == null) return NotFound();

            return View(admin);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            var admin = _context.Admins.Find(id);
            if (admin == null) return NotFound();

            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đặt lại mật khẩu thành công";

            return RedirectToAction(nameof(StaffList));
        }

        public async Task<IActionResult> EmployeeDashboard()
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            var model = new EmployeeDashboardViewModel();

            model.TotalEmployees = _context.Admins.Count();
            model.ActiveEmployees = _context.Admins.Count(x => x.IsActive);
            model.LockedEmployees = _context.Admins.Count(x => !x.IsActive);

            // Cập nhật lấy dữ liệu thống kê từ bảng Theaters
            model.TotalBranches = _context.Theaters.Count();
            model.TotalPositions = _context.Positions.Count();

            model.BranchLabels = _context.Theaters.Select(x => x.Name).ToList();
            model.BranchValues = _context.Theaters
                .Select(x => _context.Admins.Count(a => a.BranchId == x.TheaterId))
                .ToList();

            model.PositionLabels = _context.Positions.Select(x => x.PositionName).ToList();
            model.PositionValues = _context.Positions
                .Select(x => _context.Admins.Count(a => a.PositionId == x.PositionId))
                .ToList();

            model.RecentLogins = _context.Admins
                .OrderByDescending(x => x.LastLogin)
                .Take(10)
                .Include(x => x.Branch)
                .Include(x => x.Position)
                .ToList();

            return View(model);
        }

        public async Task<IActionResult> ActivityLogs(string search, string action)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            var query = _context.ActivityLogs
                .AsNoTracking()
                .Include(x => x.Admin)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Entity.Contains(search));

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(x => x.Action == action);

            var data = await query.OrderByDescending(x => x.LogDate).ToListAsync();
            ViewBag.Count = data.Count;

            return View(data);
        }


        // --- MODULE CHẤM CÔNG ---
        // 1. Hàm hiển thị (Có chọn ngày)
        // --- MODULE CHẤM CÔNG ---
        // 1. Hàm hiển thị (Có chọn ngày & Phân quyền)
        [HttpGet]
        public IActionResult CheckInOut(DateTime? date)
        {
            // 1. Kiểm tra đăng nhập và lấy thông tin người dùng hiện tại
            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login");

            int myAdminId = int.Parse(adminIdStr);
            bool isSuperAdmin = HttpContext.Session.GetString("Role") == "SuperAdmin";

            DateTime targetDate = date ?? DateTime.Now.Date;
            ViewBag.SelectedDate = targetDate;

            // 2. Tạo câu truy vấn cơ bản (chưa thực thi)
            var attendanceQuery = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Date == targetDate.Date);

            var payrollQuery = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Month == targetDate.Month && a.Date.Year == targetDate.Year && a.IsApproved == true);

            // 3. PHÂN QUYỀN: Nếu KHÔNG PHẢI SuperAdmin thì chỉ lấy dữ liệu của chính mình
            if (!isSuperAdmin)
            {
                attendanceQuery = attendanceQuery.Where(a => a.AdminId == myAdminId);
                payrollQuery = payrollQuery.Where(a => a.AdminId == myAdminId);
            }

            var attendanceList = attendanceQuery.ToList();

            // Lấy dữ liệu lên RAM trước khi tính toán phức tạp
            var rawPayrollData = payrollQuery.ToList();

            var payrollList = rawPayrollData
                .GroupBy(a => a.AdminId)
                .Select(g =>
                {
                    // Chỉ cộng thời gian của những bản ghi có đầy đủ Giờ Vào và Giờ Ra
                    var validRecords = g.Where(x => x.CheckInTime.HasValue && x.CheckOutTime.HasValue);
                    long totalTicks = validRecords.Sum(x => (x.CheckOutTime.Value - x.CheckInTime.Value).Ticks);
                    TimeSpan totalTime = TimeSpan.FromTicks(totalTicks);

                    return new PayrollViewModel
                    {
                        FullName = g.FirstOrDefault().Admin.FullName,
                        EmployeeCode = g.FirstOrDefault().Admin.EmployeeCode,
                        TotalDays = g.Select(x => x.Date.Date).Distinct().Count(),
                        // Định dạng chuỗi: Tính tổng số giờ (có thể > 24h), phút, giây
                        TotalTimeFormatted = $"{(int)totalTime.TotalHours} giờ {totalTime.Minutes} phút {totalTime.Seconds} giây"
                    };
                }).ToList();

            var model = new Tuple<IEnumerable<Attendance>, IEnumerable<PayrollViewModel>>(attendanceList, payrollList);
            return View(model);
        }

        // 2. Hàm duyệt tất cả trong ngày
        [HttpPost]
        public async Task<IActionResult> ApproveAll(DateTime date)
        {
            if (HttpContext.Session.GetString("Role") != "SuperAdmin") return Unauthorized();

            var records = await _context.Attendance
                .Where(a => a.Date.Date == date.Date && !a.IsApproved)
                .ToListAsync();

            foreach (var item in records) item.IsApproved = true;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        public IActionResult PayrollReport(int? month, int? year)
        {
            int m = month ?? DateTime.Now.Month;
            int y = year ?? DateTime.Now.Year;

            // Lấy dữ liệu thô lên trước
            var rawData = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Month == m && a.Date.Year == y && a.IsApproved == true)
                .ToList();

            var report = rawData
                .GroupBy(a => a.AdminId)
                .Select(g =>
                {
                    var validRecords = g.Where(x => x.CheckInTime.HasValue && x.CheckOutTime.HasValue);
                    long totalTicks = validRecords.Sum(x => (x.CheckOutTime.Value - x.CheckInTime.Value).Ticks);
                    TimeSpan totalTime = TimeSpan.FromTicks(totalTicks);

                    return new PayrollViewModel
                    {
                        FullName = g.FirstOrDefault().Admin.FullName,
                        EmployeeCode = g.FirstOrDefault().Admin.EmployeeCode,
                        TotalDays = g.Select(x => x.Date.Date).Distinct().Count(),
                        TotalTimeFormatted = $"{(int)totalTime.TotalHours} giờ {totalTime.Minutes} phút {totalTime.Seconds} giây"
                    };
                })
                .ToList();

            ViewBag.Month = m;
            ViewBag.Year = y;
            return View(report);
        }

        // --- MODULE NGHỈ PHÉP CẬP NHẬT ---

        [HttpGet]
        public IActionResult LeaveRequest()
        {
            // Nạp danh sách loại nghỉ phép vào ViewBag

            ViewBag.LeaveTypes = new SelectList(_context.LeaveTypes.ToList(), "LeaveTypeId", "TypeName");

            var adminId = int.Parse(HttpContext.Session.GetString("AdminId"));
            var myRequests = _context.LeaveRequests
                .Include(l => l.LeaveType) // Bao gồm loại nghỉ để hiển thị tên
                .Where(l => l.AdminId == adminId)
                .OrderByDescending(l => l.CreatedAt)
                .ToList();

            return View(myRequests);
        }

        [HttpPost]
        public async Task<IActionResult> LeaveRequest(LeaveRequest model)
        {
            model.AdminId = int.Parse(HttpContext.Session.GetString("AdminId"));
            model.Status = "Chờ duyệt";
            model.CreatedAt = DateTime.Now;

            _context.LeaveRequests.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã gửi đơn nghỉ phép!";
            return RedirectToAction("LeaveRequest"); // Quay lại trang đăng ký để xem danh sách
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLeaveType(int id)
        {
            var type = await _context.LeaveTypes.FindAsync(id);
            if (type != null)
            {
                _context.LeaveTypes.Remove(type);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageLeaveSettings"); // Điều hướng về trang quản lý loại nghỉ
        }
        // Admin xem và duyệt đơn
        public IActionResult ManageLeave()
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            // Thêm Include để lấy tên loại nghỉ
            var requests = _context.LeaveRequests
                .Include(l => l.Admin)
                .Include(l => l.LeaveType)
                .ToList();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int id, string status)
        {
            // Thêm dòng kiểm tra quyền này
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard");

            var request = _context.LeaveRequests.Find(id);
            if (request != null)
            {
                request.Status = status; // "Đã duyệt" hoặc "Từ chối"
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageLeave");
        }
        public class AttendanceDto
        {
            public string type { get; set; }
            public string imageBase64 { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheck([FromBody] AttendanceDto model)
        {
            var adminIdString = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdString)) return Unauthorized();

            var adminId = int.Parse(adminIdString);
            DateTime today = DateTime.Now.Date;
            DateTime currentTime = DateTime.Now;

            // --- BƯỚC 1: KIỂM TRA ĐIỀU KIỆN PHÂN CA (CHỈ ÁP DỤNG CHO CHECK-IN) ---
            if (model.type == "in")
            {
                // Kiểm tra xem nhân viên có ca làm việc trong ngày hôm nay không
                bool hasScheduleToday = await _context.WorkSchedules
                    .AnyAsync(ws => ws.AdminId == adminId && ws.WorkDate.Date == today);

                if (!hasScheduleToday)
                {
                    // Trả về JSON báo lỗi để Frontend hiển thị thông báo
                    return Json(new { success = false, message = "Bạn chưa được phân ca làm việc trong ngày hôm nay nên không thể chấm công!" });
                }
            }

            // --- BƯỚC 2: LƯU FILE ẢNH (Chỉ thực hiện khi đã qua được bước kiểm tra) ---
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/attendance");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = $"{adminId}_{currentTime:yyyyMMddHHmmss}.png";
            string path = Path.Combine(folder, fileName);

            try
            {
                byte[] bytes = Convert.FromBase64String(model.imageBase64.Split(',')[1]);
                System.IO.File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu ảnh chấm công: " + ex.Message });
            }

            // --- BƯỚC 3: XỬ LÝ LOGIC CHECK-IN / CHECK-OUT ---
            if (model.type == "in")
            {
                // Check-in -> Luôn tạo dòng mới
                var record = new Attendance
                {
                    AdminId = adminId,
                    Date = today,
                    CheckInTime = currentTime,
                    CheckInPhoto = "/uploads/attendance/" + fileName
                };
                _context.Attendance.Add(record);
            }
            else if (model.type == "out")
            {
                // Check-out -> TÌM DÒNG CHECK-IN GẦN NHẤT CHƯA CÓ GIỜ RA để cập nhật
                var existingRecord = await _context.Attendance
                    .Where(a => a.AdminId == adminId && a.Date.Date == today && a.CheckOutTime == null)
                    .OrderByDescending(a => a.CheckInTime)
                    .FirstOrDefaultAsync();

                if (existingRecord != null)
                {
                    // Cập nhật giờ ra
                    existingRecord.CheckOutTime = currentTime;
                    existingRecord.CheckOutPhoto = "/uploads/attendance/" + fileName;
                    _context.Attendance.Update(existingRecord);
                }
                else
                {
                    // Trường hợp nhân viên quên check-in mà bấm check-out luôn
                    var newRecord = new Attendance
                    {
                        AdminId = adminId,
                        Date = today,
                        CheckOutTime = currentTime,
                        CheckOutPhoto = "/uploads/attendance/" + fileName
                    };
                    _context.Attendance.Add(newRecord);
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Chấm công thành công!" });
        }
        [HttpGet]
        public IActionResult AttendanceHistory(DateTime? date)
        {
            // Lấy ngày truyền vào, nếu không có thì mặc định là hôm nay
            DateTime targetDate = date ?? DateTime.Now.Date;

            var logs = _context.Attendance
                .Include(a => a.Admin) // Cần Include để lấy tên Admin
                .Where(a => a.Date.Date == targetDate.Date)
                .OrderByDescending(a => a.CheckInTime)
                .ToList();

            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");
            return View(logs);
        }

      
        [HttpPost]
        public async Task<IActionResult> ApproveAttendance(int id)
        {
            // Kiểm tra quyền: Chỉ SuperAdmin mới được duyệt
            if (HttpContext.Session.GetString("Role") != "SuperAdmin")
            {
                return Json(new { success = false, message = "Bạn không có quyền này!" });
            }

            var record = await _context.Attendance.FindAsync(id);
            if (record == null)
            {
                return Json(new { success = false, message = "Không tìm thấy dữ liệu chấm công!" });
            }

            record.IsApproved = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        // =====================================================
        // ============ TRACKING NGƯỜI DÙNG (KHÁCH HÀNG) =========
        // =====================================================

        public async Task<IActionResult> CustomerActivityLogs(string activityType, int page = 1)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            int pageSize = 50;

            var query = _context.UserActivityLogs
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Movie)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(activityType))
                query = query.Where(x => x.ActivityType == activityType);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.ActivityType = activityType;
            ViewBag.ActivityTypes = await _context.UserActivityLogs
                .Select(x => x.ActivityType)
                .Distinct()
                .ToListAsync();

            return View(data);
        }

        public async Task<IActionResult> MovieViewStats()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var data = await _context.UserMovieViews
                .Include(v => v.Movie)
                .GroupBy(v => new { v.MovieId, v.Movie.Title, v.Movie.PosterUrl })
                .Select(g => new
                {
                    g.Key.MovieId,
                    g.Key.Title,
                    g.Key.PosterUrl,
                    TotalViews = g.Sum(x => x.ViewCount),
                    UniqueViewers = g.Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(30)
                .ToListAsync();

            return View(data);
        }

        public async Task<IActionResult> CustomerSearchLogs(int page = 1)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            int pageSize = 50;

            var query = _context.UserSearchLogs
                .AsNoTracking()
                .Include(x => x.Customer)
                .OrderByDescending(x => x.CreatedAt);

            var total = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(data);
        }

        public async Task<IActionResult> TopSearchKeywords()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var data = await _context.UserSearchLogs
                .GroupBy(s => s.Keyword.ToLower())
                .Select(g => new
                {
                    Keyword = g.Key,
                    SearchCount = g.Count(),
                    AvgResultCount = g.Average(x => x.ResultCount ?? 0)
                })
                .OrderByDescending(x => x.SearchCount)
                .Take(30)
                .ToListAsync();

            return View(data);
        }

        // Xem khách hàng nào đang được gợi ý phim gì (test nhanh không cần login vào tài khoản khách)
        public async Task<IActionResult> CustomerRecommendPreview(int customerId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var genreScores = await _context.Genres
                .Select(g => new
                {
                    g.GenreId,
                    g.Name,
                    Score =
                        _context.Tickets.Count(t =>
                            t.Showtime.Movie.Genres.Any(mg => mg.GenreId == g.GenreId) &&
                            t.Order!.CustomerId == customerId) * 3
                        +
                        (_context.UserMovieViews
                            .Where(v => v.CustomerId == customerId &&
                                        v.Movie.Genres.Any(mg => mg.GenreId == g.GenreId))
                            .Sum(v => (int?)v.ViewCount) ?? 0)
                })
                .OrderByDescending(x => x.Score)
                .ToListAsync();

            var topGenreIds = genreScores.Take(3).Select(x => x.GenreId).ToList();

            var recommended = await _context.Movies
                .Where(m => m.IsActive == true &&
                            m.Genres.Any(g => topGenreIds.Contains(g.GenreId)))
                .OrderByDescending(m => m.ReleaseDate)
                .Take(10)
                .ToListAsync();

            ViewBag.CustomerId = customerId;
            ViewBag.GenreScores = genreScores;
            ViewBag.Customer = await _context.Customers.FindAsync(customerId);

            return View(recommended);
        }
        // GET: Admin/MySchedule
        public async Task<IActionResult> MySchedule(DateTime? startDate)
        {
            // 1. Kiểm tra trạng thái đăng nhập
            if (!IsLoggedIn()) return RedirectToAction("Login");

            // 2. Lấy AdminId của người đang đăng nhập từ Session
            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login");

            int myAdminId = int.Parse(adminIdStr);

            // 3. Tính toán ngày bắt đầu của tuần (Thứ 2)
            DateTime today = DateTime.Today;
            DateTime startOfWeek = startDate ?? today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            // Xử lý riêng cho ngày Chủ Nhật (C# quy ước Sunday = 0)
            if (today.DayOfWeek == DayOfWeek.Sunday && startDate == null)
            {
                startOfWeek = today.AddDays(-6);
            }

            // 4. Tạo danh sách 7 ngày trong tuần
            List<DateTime> weekDates = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                weekDates.Add(startOfWeek.AddDays(i));
            }
            DateTime endOfWeek = weekDates.Last();

            // 5. CHỈ QUERY LỊCH CỦA NHÂN VIÊN ĐANG ĐĂNG NHẬP
            var mySchedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.AdminId == myAdminId && ws.WorkDate >= startOfWeek && ws.WorkDate <= endOfWeek)
                .OrderBy(ws => ws.WorkDate)
                .ThenBy(ws => ws.Shift.StartTime)
                .ToListAsync();

            // Truyền dữ liệu ra View
            ViewBag.WeekDates = weekDates;
            ViewBag.CurrentStart = startOfWeek;

            return View(mySchedules);
        }

    }
}