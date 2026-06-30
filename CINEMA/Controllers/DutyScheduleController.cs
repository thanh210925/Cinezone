using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class DutyScheduleController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public DutyScheduleController(CinemaContext context)
        {
            _context = context;
        }

        // GET: DutySchedule
        // Thay thế hàm Index cũ bằng hàm này
        public async Task<IActionResult> Index(DateTime? startDate)
        {
            // 1. Xác định ngày bắt đầu của tuần (Thứ 2)
            DateTime today = DateTime.Today;

            // Nếu người dùng không truyền startDate, lấy Thứ 2 của tuần hiện tại
            DateTime startOfWeek = startDate ?? today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            // Xử lý riêng cho ngày Chủ Nhật (C# quy ước Sunday = 0)
            if (today.DayOfWeek == DayOfWeek.Sunday && startDate == null)
            {
                startOfWeek = today.AddDays(-6);
            }

            // 2. Tạo danh sách 7 ngày trong tuần để truyền ra View
            List<DateTime> weekDates = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                weekDates.Add(startOfWeek.AddDays(i));
            }
            DateTime endOfWeek = weekDates.Last();

            // 3. Lấy danh sách Nhân viên kèm theo Lịch làm việc CỦA TUẦN ĐÓ
            var employees = await _context.Admins
                .Where(a => a.IsActive)
                // Lọc lịch làm việc chỉ trong phạm vi tuần đang xem
                .Include(a => a.WorkSchedules.Where(ws => ws.WorkDate >= startOfWeek && ws.WorkDate <= endOfWeek))
                    .ThenInclude(ws => ws.Shift)
                .ToListAsync();

            // Truyền dữ liệu ngày tháng ra View
            ViewBag.WeekDates = weekDates;
            ViewBag.CurrentStart = startOfWeek;

            return View(employees);
        }

        // GET: DutySchedule/Create
        public IActionResult Create()
        {
            // Chỉ hiển thị các nhân viên đang hoạt động (IsActive = true)
            ViewData["AdminId"] = new SelectList(_context.Admins.Where(a => a.IsActive), "AdminId", "FullName");
            ViewData["ShiftId"] = new SelectList(_context.Shifts, "ShiftId", "ShiftName");
            return View();
        }

        // POST: DutySchedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ScheduleId,AdminId,ShiftId,WorkDate")] WorkSchedule workSchedule)
        {
            // Loại bỏ kiểm tra xác thực trên thuộc tính điều hướng để tránh lỗi ModelState.IsValid = false
            ModelState.Remove("Admin");
            ModelState.Remove("Shift");

            if (ModelState.IsValid)
            {
                _context.Add(workSchedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Nếu có lỗi, nạp lại danh sách Dropdown để form không bị trống
            ViewData["AdminId"] = new SelectList(_context.Admins.Where(a => a.IsActive), "AdminId", "FullName", workSchedule.AdminId);
            ViewData["ShiftId"] = new SelectList(_context.Shifts, "ShiftId", "ShiftName", workSchedule.ShiftId);
            return View(workSchedule);
        }

        // GET: DutySchedule/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var workSchedule = await _context.WorkSchedules.FindAsync(id);
            if (workSchedule == null) return NotFound();

            // Truyền giá trị hiện tại vào SelectList để Dropdown chọn đúng phần tử đó
            ViewData["AdminId"] = new SelectList(_context.Admins.Where(a => a.IsActive), "AdminId", "FullName", workSchedule.AdminId);
            ViewData["ShiftId"] = new SelectList(_context.Shifts, "ShiftId", "ShiftName", workSchedule.ShiftId);

            return View(workSchedule);
        }

        // POST: DutySchedule/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ScheduleId,AdminId,ShiftId,WorkDate")] WorkSchedule workSchedule)
        {
            if (id != workSchedule.ScheduleId) return NotFound();

            ModelState.Remove("Admin");
            ModelState.Remove("Shift");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(workSchedule);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WorkScheduleExists(workSchedule.ScheduleId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["AdminId"] = new SelectList(_context.Admins.Where(a => a.IsActive), "AdminId", "FullName", workSchedule.AdminId);
            ViewData["ShiftId"] = new SelectList(_context.Shifts, "ShiftId", "ShiftName", workSchedule.ShiftId);
            return View(workSchedule);
        }

        // POST: DutySchedule/Delete/5
        // Thiết kế theo dạng Delete trực tiếp giống như Shift
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var workSchedule = await _context.WorkSchedules.FindAsync(id);
            if (workSchedule != null)
            {
                _context.WorkSchedules.Remove(workSchedule);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool WorkScheduleExists(int id)
        {
            return _context.WorkSchedules.Any(e => e.ScheduleId == id);
        }
    }
}