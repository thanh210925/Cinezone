using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    [Route("Admin/[action]")]
    public class AdminStaffController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminStaffController(CinemaContext context)
        {
            _context = context;
        }

        private bool IsSuperAdmin()
        {
            return HttpContext.Session.GetString("Role") == "SuperAdmin";
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

        public async Task<IActionResult> StaffList(string search)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.SuperAdminCount = await _context.Admins.CountAsync(x => x.Role == "SuperAdmin");
            ViewBag.ManagerCount = await _context.Admins.CountAsync(x => x.Role == "Manager");
            ViewBag.CrmCount = await _context.Admins.CountAsync(x => x.Role == "CRM" || x.Role == "CrmStaff");
            ViewBag.StaffCount = await _context.Admins.CountAsync(x => x.Role == "Staff" || x.Role == "TicketStaff" || x.Role == null || x.Role == "");

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
                    x.Email.Contains(search) ||
                    (x.EmployeeCode != null && x.EmployeeCode.Contains(search)));
            }

            return View("~/Views/Admin/StaffList.cshtml", await query.ToListAsync());
        }

        public async Task<IActionResult> CreateStaff()
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name");
            ViewBag.Positions = new SelectList(_context.Positions.ToList(), "PositionId", "PositionName");

            return View("~/Views/Admin/CreateStaff.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> CreateStaff(Admin admin, IFormFile avatar)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền!";
                return RedirectToAction("Dashboard", "Admin");
            }

            ModelState.Remove("Branch");
            ModelState.Remove("Position");
            ModelState.Remove("Theater");
            ModelState.Remove("EmployeeCode");
            ModelState.Remove("Avatar");

            if (ModelState.IsValid)
            {
                try
                {
                    admin.EmployeeCode = GenerateEmployeeCode();

                    if (string.IsNullOrEmpty(admin.PasswordHash))
                    {
                        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");
                    }
                    else
                    {
                        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(admin.PasswordHash);
                    }

                    if (avatar != null && avatar.Length > 0)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatar.FileName);
                        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await avatar.CopyToAsync(stream);
                        }
                        admin.Avatar = "/images/" + fileName;
                    }

                    admin.CreatedAt = DateTime.Now;
                    admin.IsActive = true;

                    _context.Admins.Add(admin);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Thêm nhân viên thành công!";
                    return RedirectToAction("StaffList");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Lỗi khi thêm nhân viên: " + ex.Message;
                }
            }

            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name", admin.BranchId);
            ViewBag.Positions = new SelectList(_context.Positions.ToList(), "PositionId", "PositionName", admin.PositionId);
            return View("~/Views/Admin/CreateStaff.cshtml", admin);
        }

        public async Task<IActionResult> EditStaff(int id)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(id);
            if (admin == null) return NotFound();

            ViewBag.Branches = new SelectList(_context.Theaters.ToList(), "TheaterId", "Name", admin.BranchId);
            ViewBag.Positions = new SelectList(_context.Positions.ToList(), "PositionId", "PositionName", admin.PositionId);

            return View("~/Views/Admin/EditStaff.cshtml", admin);
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(Admin model, IFormFile avatar)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(model.AdminId);
            if (admin == null) return NotFound();

            admin.FullName = model.FullName;
            admin.Email = model.Email;
            admin.Phone = model.Phone;
            admin.Role = model.Role;
            admin.BranchId = model.BranchId;
            admin.PositionId = model.PositionId;

            if (avatar != null && avatar.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatar.FileName);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                admin.Avatar = "/images/" + fileName;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật nhân viên thành công!";
            return RedirectToAction("StaffList");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                _context.Admins.Remove(admin);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa nhân viên!";
            }
            return RedirectToAction("StaffList");
        }

        [HttpPost]
        public async Task<IActionResult> AssignRole(int id, string role)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                admin.Role = role;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Cập nhật quyền hạn cho tài khoản {admin.FullName} thành công!";
            }
            return RedirectToAction("StaffList");
        }

        [HttpPost]
        public async Task<IActionResult> SavePermissions(int id, string role, List<string> selectedPermissions)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                admin.Role = role;
                admin.Permissions = selectedPermissions != null && selectedPermissions.Any()
                    ? string.Join(",", selectedPermissions)
                    : "";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Lưu thay đổi cấp quyền cho Admin/Nhân viên {admin.FullName} thành công!";
            }
            return RedirectToAction("StaffList");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStaffStatus(int id)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                admin.IsActive = !admin.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("StaffList");
        }

        public async Task<IActionResult> ActivityLogs(string search, string action)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

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

            return View("~/Views/Admin/ActivityLogs.cshtml", data);
        }
    }
}
