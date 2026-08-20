using CINEMA.Models;
using CINEMA.ViewModels;
using CINEMA.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    [Route("Admin/[action]")]
    public class AdminAttendanceController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminAttendanceController(CinemaContext context)
        {
            _context = context;
        }

        private bool IsSuperAdmin()
        {
            return HttpContext.Session.GetString("Role") == "SuperAdmin";
        }

        [HttpGet]
        public IActionResult CheckInOut(DateTime? date)
        {
            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login", "Admin");

            int myAdminId = int.Parse(adminIdStr);
            bool isSuperAdmin = HttpContext.Session.GetString("Role") == "SuperAdmin";

            var currentAdmin = _context.Admins.FirstOrDefault(a => a.AdminId == myAdminId);
            ViewBag.FacePhoto = currentAdmin?.FacePhoto;

            DateTime targetDate = date ?? DateTime.Now.Date;
            ViewBag.SelectedDate = targetDate;

            var attendanceQuery = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Date == targetDate.Date);

            var payrollQuery = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Month == targetDate.Month && a.Date.Year == targetDate.Year && a.IsApproved == true);

            if (!isSuperAdmin)
            {
                attendanceQuery = attendanceQuery.Where(a => a.AdminId == myAdminId);
                payrollQuery = payrollQuery.Where(a => a.AdminId == myAdminId);
            }

            var attendanceList = attendanceQuery.ToList();

            var firstDayOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            var monthAttendanceQuery = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date >= firstDayOfMonth && a.Date <= lastDayOfMonth);
            if (!isSuperAdmin)
            {
                monthAttendanceQuery = monthAttendanceQuery.Where(a => a.AdminId == myAdminId);
            }
            ViewBag.MonthAttendance = monthAttendanceQuery.ToList();

            var rawPayrollData = payrollQuery.ToList();

            var payrollList = rawPayrollData
                .GroupBy(a => a.AdminId)
                .Select(g =>
                {
                    var validRecords = g.Where(x => x.CheckInTime.HasValue && x.CheckOutTime.HasValue);
                    long totalTicks = validRecords.Sum(x => (x.CheckOutTime.Value - x.CheckInTime.Value).Ticks);
                    TimeSpan totalTime = TimeSpan.FromTicks(totalTicks);

                    return new PayrollViewModel
                    {
                        FullName = g.FirstOrDefault()?.Admin?.FullName ?? "N/A",
                        EmployeeCode = g.FirstOrDefault()?.Admin?.EmployeeCode ?? "N/A",
                        TotalDays = g.Select(x => x.Date.Date).Distinct().Count(),
                        TotalTimeFormatted = $"{(int)totalTime.TotalHours} giờ {totalTime.Minutes} phút {totalTime.Seconds} giây"
                    };
                }).ToList();

            var model = new Tuple<IEnumerable<Attendance>, IEnumerable<PayrollViewModel>>(attendanceList, payrollList);
            return View("~/Views/Admin/CheckInOut.cshtml", model);
        }

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

        [HttpGet]
        public IActionResult DailyAttendanceReport(DateTime? date)
        {
            DateTime targetDate = date ?? DateTime.Now.Date;

            var dailyData = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Date == targetDate.Date)
                .OrderByDescending(a => a.CheckInTime)
                .ToList();

            ViewBag.SelectedDate = targetDate;
            return View("~/Views/Admin/DailyAttendanceReport.cshtml", dailyData);
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyTimesheet(int? month, int? year)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Dashboard", "Admin");
            }

            int targetMonth = month ?? DateTime.Now.Month;
            int targetYear = year ?? DateTime.Now.Year;

            var admins = await _context.Admins.ToListAsync();
            int daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = new DateTime(targetYear, targetMonth, daysInMonth);

            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.WorkDate >= startDate && ws.WorkDate <= endDate)
                .ToListAsync();

            var attendances = await _context.Attendance
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Where(l => l.Status == "Đã duyệt" && l.StartDate <= endDate && l.EndDate >= startDate)
                .ToListAsync();

            var timesheetData = new List<EmployeeTimesheetDto>();

            foreach (var emp in admins)
            {
                var dto = new EmployeeTimesheetDto
                {
                    AdminId = emp.AdminId ?? 0,
                    FullName = emp.FullName ?? "Nhân viên #" + emp.AdminId,
                    Role = emp.Role ?? "N/A"
                };

                for (int day = 1; day <= daysInMonth; day++)
                {
                    var currentDate = new DateTime(targetYear, targetMonth, day);
                    var empSchedules = schedules.Where(s => s.AdminId == emp.AdminId && s.WorkDate.Date == currentDate.Date).ToList();
                    bool isScheduled = empSchedules.Any();

                    bool isOnLeave = leaves.Any(l => l.AdminId == emp.AdminId && currentDate.Date >= l.StartDate.Date && currentDate.Date <= l.EndDate.Date);
                    var att = attendances.FirstOrDefault(a => a.AdminId == emp.AdminId && a.Date.Date == currentDate.Date);

                    string statusSymbol = "-";

                    if (isScheduled)
                    {
                        dto.TotalShifts += empSchedules.Count;

                        if (isOnLeave)
                        {
                            statusSymbol = "P";
                            dto.TotalLeaveDays++;
                        }
                        else if (att == null || (!att.CheckInTime.HasValue && !att.CheckOutTime.HasValue))
                        {
                            statusSymbol = "V";
                            dto.TotalAbsentDays++;
                        }
                        else
                        {
                            dto.TotalWorked++;
                            bool isLate = false;
                            bool isEarly = false;

                            var sortedShifts = empSchedules.Select(s => s.Shift).OrderBy(s => s.StartTime).ToList();
                            var firstShift = sortedShifts.First();
                            var lastShift = sortedShifts.Last();

                            if (att.CheckInTime.HasValue)
                            {
                                var checkInTimeOfDay = att.CheckInTime.Value.TimeOfDay;
                                if (checkInTimeOfDay > firstShift.StartTime)
                                {
                                    isLate = true;
                                    double diff = (checkInTimeOfDay - firstShift.StartTime).TotalMinutes;
                                    dto.TotalLateMinutes += diff;
                                }
                            }
                            else
                            {
                                isLate = true;
                            }

                            if (att.CheckOutTime.HasValue)
                            {
                                var checkOutTimeOfDay = att.CheckOutTime.Value.TimeOfDay;
                                if (checkOutTimeOfDay < lastShift.EndTime)
                                {
                                    isEarly = true;
                                    double diff = (lastShift.EndTime - checkOutTimeOfDay).TotalMinutes;
                                    dto.TotalEarlyMinutes += diff;
                                }
                            }
                            else
                            {
                                isEarly = true;
                            }

                            if (isLate && isEarly)
                            {
                                statusSymbol = "M/S";
                                dto.TotalLateDays++;
                                dto.TotalEarlyDays++;
                            }
                            else if (isLate)
                            {
                                statusSymbol = "M";
                                dto.TotalLateDays++;
                            }
                            else if (isEarly)
                            {
                                statusSymbol = "S";
                                dto.TotalEarlyDays++;
                            }
                            else
                            {
                                statusSymbol = "X";
                            }

                            if (!att.IsApproved)
                            {
                                statusSymbol += "?";
                            }
                        }
                    }
                    else
                    {
                        if (att != null && (att.CheckInTime.HasValue || att.CheckOutTime.HasValue))
                        {
                            statusSymbol = "TC";
                            dto.TotalWorked++;
                        }
                    }

                    dto.DailyStatus[day] = statusSymbol;
                }

                timesheetData.Add(dto);
            }

            ViewBag.SelectedMonth = targetMonth;
            ViewBag.SelectedYear = targetYear;
            ViewBag.DaysInMonth = daysInMonth;

            return View("~/Views/Admin/MonthlyTimesheet.cshtml", timesheetData);
        }

        public IActionResult GetAttendanceDetail(int adminId, int month, int year)
        {
            var details = _context.Attendance
                .Where(a => a.AdminId == adminId && a.Date.Month == month && a.Date.Year == year)
                .OrderByDescending(a => a.Date)
                .ToList();

            if (details == null || !details.Any())
            {
                return Content("<p class='text-center text-muted'>Không tìm thấy dữ liệu chấm công.</p>");
            }

            return PartialView("_AttendanceDetail", details);
        }

        [HttpGet]
        public IActionResult PayrollReport(DateTime? date)
        {
            DateTime targetDate = date ?? DateTime.Now.Date;

            var dailyDetails = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Date == targetDate.Date)
                .OrderByDescending(a => a.CheckInTime)
                .ToList();

            ViewBag.SelectedDate = targetDate;
            return View("~/Views/Admin/PayrollReport.cshtml", dailyDetails);
        }

        public class AttendanceDto
        {
            public string type { get; set; } = null!;
            public string imageBase64 { get; set; } = null!;
            public bool? isFaceMatched { get; set; }
        }

        public class SaveFaceDto
        {
            public int AdminId { get; set; }
            public string imageBase64 { get; set; } = null!;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessCheck([FromBody] AttendanceDto model)
        {
            if (model == null) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var adminIdString = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdString)) return Unauthorized();

            var adminId = int.Parse(adminIdString);
            var today = DateTime.Now.Date;
            var currentTime = DateTime.Now;
            var shouldApprove = model.isFaceMatched == true;

            var schedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.AdminId == adminId && ws.WorkDate.Date == today)
                .ToListAsync();

            if (!schedules.Any())
                return Json(new { success = false, message = "Bạn không có ca làm việc hôm nay!" });

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "attendance");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = $"{adminId}_{currentTime:yyyyMMddHHmmss}.png";
            string path = Path.Combine(folder, fileName);

            try
            {
                byte[] bytes = Convert.FromBase64String(model.imageBase64.Split(',')[1]);
                await System.IO.File.WriteAllBytesAsync(path, bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving attendance photo: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi lưu ảnh chấm công." });
            }

            var existingRecord = await _context.Attendance
                .FirstOrDefaultAsync(a => a.AdminId == adminId && a.Date.Date == today);

            var currentTimeOfDay = currentTime.TimeOfDay;

            try
            {
                if (model.type == "in")
                {
                    if (existingRecord != null && existingRecord.CheckInTime.HasValue)
                    {
                        return Json(new { success = false, message = "Bạn đã thực hiện Check-in ngày hôm nay rồi!" });
                    }

                    bool isWithinWindow = false;
                    var sortedShifts = schedules.Select(s => s.Shift).OrderBy(s => s.StartTime).ToList();

                    foreach (var shift in sortedShifts)
                    {
                        var checkInStart = shift.StartTime.Subtract(TimeSpan.FromMinutes(30));
                        var checkInEnd = shift.StartTime.Add(TimeSpan.FromMinutes(15));

                        if (currentTimeOfDay >= checkInStart && currentTimeOfDay <= checkInEnd)
                        {
                            isWithinWindow = true;
                            break;
                        }
                    }

                    if (!isWithinWindow)
                    {
                        return Json(new { success = false, message = "Thời gian hiện tại không nằm trong khung giờ check-in cho phép." });
                    }

                    if (existingRecord != null)
                    {
                        existingRecord.CheckInTime = currentTime;
                        existingRecord.CheckInPhoto = "/uploads/attendance/" + fileName;
                        if (shouldApprove) existingRecord.IsApproved = true;
                        _context.Attendance.Update(existingRecord);
                    }
                    else
                    {
                        var record = new Attendance
                        {
                            AdminId = adminId,
                            Date = today,
                            CheckInTime = currentTime,
                            CheckInPhoto = "/uploads/attendance/" + fileName,
                            IsApproved = shouldApprove
                        };
                        _context.Attendance.Add(record);
                    }
                }
                else if (model.type == "out")
                {
                    if (existingRecord != null && existingRecord.CheckOutTime.HasValue)
                    {
                        return Json(new { success = false, message = "Bạn đã thực hiện Check-out ngày hôm nay rồi!" });
                    }
                    else if (existingRecord != null)
                    {
                        existingRecord.CheckOutTime = currentTime;
                        existingRecord.CheckOutPhoto = "/uploads/attendance/" + fileName;
                        if (shouldApprove) existingRecord.IsApproved = true;
                        _context.Attendance.Update(existingRecord);
                    }
                }
                else
                {
                    var record = new Attendance
                    {
                        AdminId = adminId,
                        Date = today,
                        CheckOutTime = currentTime,
                        CheckOutPhoto = "/uploads/attendance/" + fileName,
                        IsApproved = shouldApprove
                    };
                    _context.Attendance.Add(record);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing attendance: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi xử lý chấm công." });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Chấm công thành công!" });
        }

        [HttpGet]
        public async Task<IActionResult> RegisterFace()
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Dashboard", "Admin");
            }
            var admins = await _context.Admins
                .Include(a => a.Position)
                .ToListAsync();
            return View("~/Views/Admin/RegisterFace.cshtml", admins);
        }

        [HttpPost]
        public async Task<IActionResult> SaveFace([FromBody] SaveFaceDto model)
        {
            if (!IsSuperAdmin()) return Unauthorized();

            var admin = await _context.Admins.FindAsync(model.AdminId);
            if (admin == null) return NotFound();

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/faces");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string fileName = $"face_{model.AdminId}_{DateTime.Now:yyyyMMddHHmmss}.png";
            string path = Path.Combine(folder, fileName);

            try
            {
                byte[] bytes = Convert.FromBase64String(model.imageBase64.Split(',')[1]);
                await System.IO.File.WriteAllBytesAsync(path, bytes);

                if (!string.IsNullOrEmpty(admin.FacePhoto))
                {
                    string oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", admin.FacePhoto.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                admin.FacePhoto = "/uploads/faces/" + fileName;
                _context.Admins.Update(admin);
                await _context.SaveChangesAsync();

                return Json(new { success = true, facePhoto = admin.FacePhoto });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu khuôn mặt: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFace(int adminId)
        {
            if (!IsSuperAdmin()) return Unauthorized();

            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null) return NotFound();

            try
            {
                if (!string.IsNullOrEmpty(admin.FacePhoto))
                {
                    string oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", admin.FacePhoto.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                admin.FacePhoto = null;
                _context.Admins.Update(admin);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa khuôn mặt thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa khuôn mặt: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult AttendanceHistory(DateTime? date)
        {
            DateTime targetDate = date ?? DateTime.Now.Date;

            var logs = _context.Attendance
                .Include(a => a.Admin)
                .Where(a => a.Date.Date == targetDate.Date)
                .OrderByDescending(a => a.CheckInTime)
                .ToList();

            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");
            return View("~/Views/Admin/AttendanceHistory.cshtml", logs);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAttendance(int id)
        {
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

        public async Task<IActionResult> MySchedule(DateTime? startDate)
        {
            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return RedirectToAction("Login", "Admin");

            int myAdminId = int.Parse(adminIdStr);

            DateTime today = DateTime.Today;
            DateTime startOfWeek = startDate ?? today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            if (today.DayOfWeek == DayOfWeek.Sunday && startDate == null)
            {
                startOfWeek = today.AddDays(-6);
            }

            List<DateTime> weekDates = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                weekDates.Add(startOfWeek.AddDays(i));
            }
            DateTime endOfWeek = weekDates.Last();

            var mySchedules = await _context.WorkSchedules
                .Include(ws => ws.Shift)
                .Where(ws => ws.AdminId == myAdminId && ws.WorkDate >= startOfWeek && ws.WorkDate <= endOfWeek)
                .OrderBy(ws => ws.WorkDate)
                .ThenBy(ws => ws.Shift.StartTime)
                .ToListAsync();

            ViewBag.WeekDates = weekDates;
            ViewBag.CurrentStart = startOfWeek;

            return View("~/Views/Admin/MySchedule.cshtml", mySchedules);
        }
    }
}
