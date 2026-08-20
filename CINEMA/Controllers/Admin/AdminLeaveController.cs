using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class AdminLeaveController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminLeaveController(CinemaContext context)
        {
            _context = context;
        }

        private bool IsSuperAdmin()
        {
            return HttpContext.Session.GetString("Role") == "SuperAdmin";
        }

        [HttpGet]
        public IActionResult LeaveRequest()
        {
            ViewBag.LeaveTypes = new SelectList(_context.LeaveTypes.ToList(), "LeaveTypeId", "TypeName");

            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login", "Admin");

            var adminId = int.Parse(adminIdStr);
            var myRequests = _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Where(l => l.AdminId == adminId)
                .OrderByDescending(l => l.CreatedAt)
                .ToList();

            return View("~/Views/Admin/LeaveRequest.cshtml", myRequests);
        }

        [HttpPost]
        public async Task<IActionResult> LeaveRequest(LeaveRequest model)
        {
            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login", "Admin");

            model.AdminId = int.Parse(adminIdStr);
            model.Status = "Chờ duyệt";
            model.CreatedAt = DateTime.Now;

            _context.LeaveRequests.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã gửi đơn nghỉ phép!";
            return RedirectToAction("LeaveRequest");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLeaveType(int id)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var type = await _context.LeaveTypes.FindAsync(id);
            if (type != null)
            {
                _context.LeaveTypes.Remove(type);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageLeaveSettings", "Admin");
        }

        public IActionResult ManageLeave()
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var requests = _context.LeaveRequests
                .Include(l => l.Admin)
                .Include(l => l.LeaveType)
                .ToList();

            return View("~/Views/Admin/ManageLeave.cshtml", requests);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int id, string status)
        {
            if (!IsSuperAdmin()) return RedirectToAction("Dashboard", "Admin");

            var request = _context.LeaveRequests.Find(id);
            if (request != null)
            {
                request.Status = status;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageLeave");
        }
    }
}
