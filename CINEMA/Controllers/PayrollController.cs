using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;
using System.Drawing;

namespace CINEMA.Controllers
{
    public class PayrollController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public PayrollController(CinemaContext context)
        {
            _context = context;
        }

        // Kiểm tra quyền SuperAdmin trước mỗi action
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            var role = HttpContext.Session.GetString("Role");
            if (role != "SuperAdmin")
            {
                context.Result = RedirectToAction("Dashboard", "Admin");
            }
        }

        // GET: Payroll
        public async Task<IActionResult> Index(int? month, int? year, string search)
        {
            int m = month ?? DateTime.Now.Month;
            int y = year ?? DateTime.Now.Year;

            var query = _context.Payrolls
                .Include(p => p.Admin)
                .ThenInclude(a => a.Position)
                .Where(p => p.Month == m && p.Year == y);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Admin.FullName.Contains(search) || p.Admin.EmployeeCode.Contains(search));
            }

            var payrolls = await query.ToListAsync();

            // Tính toán tổng quan thẻ thống kê
            ViewBag.TotalSalary = payrolls.Sum(p => p.TotalSalary);
            ViewBag.PaidSalary = payrolls.Where(p => p.Status == "Paid").Sum(p => p.TotalSalary);
            ViewBag.UnpaidSalary = payrolls.Where(p => p.Status != "Paid").Sum(p => p.TotalSalary);
            ViewBag.EmployeeCount = payrolls.Select(p => p.AdminId).Distinct().Count();

            ViewBag.SelectedMonth = m;
            ViewBag.SelectedYear = y;
            ViewBag.SearchQuery = search;

            return View(payrolls);
        }

        // GET: Payroll/Calculate
        public IActionResult Calculate()
        {
            ViewBag.Month = DateTime.Now.Month;
            ViewBag.Year = DateTime.Now.Year;
            return View();
        }

        // POST: Payroll/Calculate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(int month, int year, decimal baseSalaryManager, decimal baseSalaryStaff)
        {
            if (baseSalaryManager <= 0 || baseSalaryStaff <= 0)
            {
                ModelState.AddModelError("", "Mức lương cơ bản mỗi giờ công phải lớn hơn 0");
                ViewBag.Month = month;
                ViewBag.Year = year;
                return View();
            }

            // Lấy danh sách nhân viên đang hoạt động
            var admins = await _context.Admins
                .Include(a => a.Position)
                .Where(a => a.IsActive)
                .ToListAsync();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Truy vấn dữ liệu chấm công đã duyệt của tháng này
            var attendanceLogs = await _context.Attendance
                .Where(a => a.Date >= startDate && a.Date <= endDate && a.IsApproved == true)
                .ToListAsync();

            // Truy vấn đơn xin nghỉ đã duyệt của tháng này
            var approvedLeaves = await _context.LeaveRequests
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Đã duyệt" && l.StartDate <= endDate && l.EndDate >= startDate)
                .ToListAsync();

            // Tìm những bản ghi lương đã có
            var existingPayrolls = await _context.Payrolls
                .Where(p => p.Month == month && p.Year == year)
                .ToListAsync();

            foreach (var admin in admins)
            {
                if (admin.AdminId == null) continue;

                // Kiểm tra xem đã có bản ghi lương được thanh toán chưa
                var hasPaidPayroll = existingPayrolls.Any(p => p.AdminId == admin.AdminId && p.Status == "Paid");
                if (hasPaidPayroll)
                {
                    // Giữ nguyên bản ghi đã thanh toán, không tính lại
                    continue;
                }

                // 1. Tính tổng số giờ làm việc thực tế (giờ, phút, giây) từ chấm công
                double totalSecs = attendanceLogs
                    .Where(a => a.AdminId == admin.AdminId && a.CheckInTime.HasValue && a.CheckOutTime.HasValue && a.CheckOutTime > a.CheckInTime)
                    .Sum(a => (a.CheckOutTime.Value - a.CheckInTime.Value).TotalSeconds);
                decimal workingHours = Math.Round((decimal)totalSecs / 3600m, 2);

                // 2. Tính số giờ nghỉ phép có lương (ví dụ: "Nghỉ phép năm" hoặc chứa từ "phép", mỗi ngày = 8 tiếng)
                int paidLeaveDays = 0;
                var myLeaves = approvedLeaves.Where(l => l.AdminId == admin.AdminId).ToList();
                foreach (var leave in myLeaves)
                {
                    // Lọc loại nghỉ phép năm
                    if (leave.LeaveType != null && (leave.LeaveType.TypeName.Contains("phép") || leave.LeaveType.TypeName.Contains("lương")))
                    {
                        var start = leave.StartDate < startDate ? startDate : leave.StartDate;
                        var end = leave.EndDate > endDate ? endDate : leave.EndDate;
                        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
                        {
                            paidLeaveDays++;
                        }
                    }
                }
                decimal paidLeaveHours = paidLeaveDays * 8.0m;

                // 3. Hệ số lương
                decimal coeff = admin.Position?.SalaryCoefficient ?? 1.0m;

                // Phân loại Quản lý vs Nhân viên để áp lương cơ bản
                bool isManager = (admin.Role == "SuperAdmin") || 
                                 (admin.Position != null && (admin.Position.PositionName.Contains("Quản lý") || admin.Position.PositionName.Contains("Admin")));
                
                decimal currentBaseSalary = isManager ? baseSalaryManager : baseSalaryStaff;

                // 4. Khởi tạo/Cập nhật bản ghi lương
                var payroll = existingPayrolls.FirstOrDefault(p => p.AdminId == admin.AdminId);
                bool isNew = false;
                if (payroll == null)
                {
                    payroll = new Payroll
                    {
                        AdminId = admin.AdminId.Value,
                        Month = month,
                        Year = year,
                        Status = "Unpaid",
                        CreatedAt = DateTime.Now,
                        Bonus = 0,
                        Deductions = 0,
                        Notes = ""
                    };
                    isNew = true;
                }

                payroll.WorkingHours = workingHours;
                payroll.PaidLeaveHours = paidLeaveHours;
                payroll.SalaryCoefficient = coeff;
                payroll.BaseSalaryPerHour = currentBaseSalary;
                payroll.TotalSalary = (workingHours + paidLeaveHours) * currentBaseSalary * coeff + payroll.Bonus - payroll.Deductions;
                
                if (isNew)
                {
                    _context.Payrolls.Add(payroll);
                }
                else
                {
                    _context.Payrolls.Update(payroll);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tính lương thành công cho tháng {month}/{year}!";
            return RedirectToAction(nameof(Index), new { month = month, year = year });
        }

        // GET: Payroll/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var payroll = await _context.Payrolls
                .Include(p => p.Admin)
                .ThenInclude(a => a.Position)
                .FirstOrDefaultAsync(p => p.PayrollId == id);

            if (payroll == null) return NotFound();

            if (payroll.Status == "Paid")
            {
                TempData["Error"] = "Bản ghi lương này đã được thanh toán và bị khóa, không thể chỉnh sửa!";
                return RedirectToAction(nameof(Index), new { month = payroll.Month, year = payroll.Year });
            }

            return View(payroll);
        }

        // POST: Payroll/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PayrollId,AdminId,Month,Year,WorkingHours,PaidLeaveHours,SalaryCoefficient,BaseSalaryPerHour,Bonus,Deductions,TotalSalary,Status,Notes")] Payroll payroll)
        {
            if (id != payroll.PayrollId) return NotFound();

            var existing = await _context.Payrolls.AsNoTracking().FirstOrDefaultAsync(p => p.PayrollId == id);
            if (existing == null) return NotFound();

            if (existing.Status == "Paid")
            {
                TempData["Error"] = "Bản ghi lương này đã được thanh toán và bị khóa, không thể chỉnh sửa!";
                return RedirectToAction(nameof(Index), new { month = existing.Month, year = existing.Year });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (payroll.Status == "Paid")
                    {
                        payroll.PaidAt = DateTime.Now;
                    }
                    else
                    {
                        payroll.PaidAt = null;
                    }

                    // Tính lại tổng lương trước khi lưu để đảm bảo chính xác
                    payroll.TotalSalary = (payroll.WorkingHours + payroll.PaidLeaveHours) * payroll.BaseSalaryPerHour * payroll.SalaryCoefficient + payroll.Bonus - payroll.Deductions;

                    _context.Update(payroll);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã cập nhật bản ghi lương!";
                    return RedirectToAction(nameof(Index), new { month = payroll.Month, year = payroll.Year });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Payrolls.Any(e => e.PayrollId == payroll.PayrollId)) return NotFound();
                    else throw;
                }
            }

            payroll.Admin = await _context.Admins.Include(a => a.Position).FirstOrDefaultAsync(a => a.AdminId == payroll.AdminId);
            return View(payroll);
        }

        // POST: Payroll/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var payroll = await _context.Payrolls.FindAsync(id);
            if (payroll != null)
            {
                int m = payroll.Month;
                int y = payroll.Year;

                if (payroll.Status == "Paid")
                {
                    TempData["Error"] = "Không thể xóa bản ghi lương đã thanh toán!";
                }
                else
                {
                    _context.Payrolls.Remove(payroll);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã xóa bản ghi lương!";
                }
                return RedirectToAction(nameof(Index), new { month = m, year = y });
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Payroll/MarkAsPaid/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var payroll = await _context.Payrolls.FindAsync(id);
            if (payroll == null) return NotFound();

            payroll.Status = "Paid";
            payroll.PaidAt = DateTime.Now;

            _context.Payrolls.Update(payroll);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã đánh dấu đã thanh toán lương!";
            return RedirectToAction(nameof(Index), new { month = payroll.Month, year = payroll.Year });
        }

        // POST: Payroll/Unlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id, string confirmPassword)
        {
            var payroll = await _context.Payrolls.FindAsync(id);
            if (payroll == null) return NotFound();

            var adminIdStr = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminIdStr)) return Unauthorized();

            var superAdmin = await _context.Admins.FindAsync(int.Parse(adminIdStr));
            if (superAdmin == null) return NotFound();

            bool isPasswordCorrect = false;
            try
            {
                isPasswordCorrect = BCrypt.Net.BCrypt.Verify(confirmPassword, superAdmin.PasswordHash);
            }
            catch
            {
                isPasswordCorrect = (superAdmin.PasswordHash == confirmPassword);
                if (isPasswordCorrect)
                {
                    // Tự động nâng cấp mật khẩu sang BCrypt
                    superAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(confirmPassword);
                    await _context.SaveChangesAsync();
                }
            }

            if (!isPasswordCorrect)
            {
                TempData["Error"] = "Mật khẩu xác nhận không chính xác! Không thể mở khóa bảng lương.";
                return RedirectToAction(nameof(Index), new { month = payroll.Month, year = payroll.Year });
            }

            var employee = await _context.Admins.FindAsync(payroll.AdminId);

            payroll.Status = "Unpaid";
            payroll.PaidAt = null;

            _context.Payrolls.Update(payroll);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã mở khóa bảng lương của nhân viên {employee?.FullName} thành công!";
            return RedirectToAction(nameof(Index), new { month = payroll.Month, year = payroll.Year });
        }

        // GET: Payroll/ExportToExcel
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(int month, int year, string? search)
        {
            var query = _context.Payrolls
                .Include(p => p.Admin)
                .ThenInclude(a => a.Position)
                .Where(p => p.Month == month && p.Year == year);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Admin.FullName.Contains(search) || p.Admin.EmployeeCode.Contains(search));
            }

            var payrolls = await query.ToListAsync();

            ExcelPackage.License.SetNonCommercialPersonal("CineZone");
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Luong_{month}_{year}");

                // Title
                worksheet.Cells["A1:O1"].Merge = true;
                worksheet.Cells["A1"].Value = $"BẢNG THANH TOÁN LƯƠNG NHÂN VIÊN THÁNG {month}/{year}";
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells["A1"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                
                // Subtitle
                worksheet.Cells["A2:O2"].Merge = true;
                worksheet.Cells["A2"].Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cells["A2"].Style.Font.Italic = true;
                worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Headers
                string[] headers = {
                    "STT", "Mã nhân viên", "Họ và tên", "Chức vụ", "Hệ số", 
                    "Giờ làm thực tế", "Giờ nghỉ phép", "Tổng giờ công", "Lương/giờ", 
                    "Lương công việc", "Thưởng", "Khấu trừ", "Thực nhận", "Trạng thái", "Ghi chú"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[4, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // Data
                int row = 5;
                int stt = 1;
                foreach (var p in payrolls)
                {
                    worksheet.Cells[row, 1].Value = stt++;
                    worksheet.Cells[row, 2].Value = p.Admin?.EmployeeCode;
                    worksheet.Cells[row, 3].Value = p.Admin?.FullName;
                    worksheet.Cells[row, 4].Value = p.Admin?.Position?.PositionName;
                    worksheet.Cells[row, 5].Value = p.SalaryCoefficient;
                    worksheet.Cells[row, 6].Value = p.WorkingHours;
                    worksheet.Cells[row, 7].Value = p.PaidLeaveHours;
                    worksheet.Cells[row, 8].Value = p.WorkingHours + p.PaidLeaveHours;
                    worksheet.Cells[row, 9].Value = p.BaseSalaryPerHour;
                    
                    decimal workSalary = (p.WorkingHours + p.PaidLeaveHours) * p.BaseSalaryPerHour * p.SalaryCoefficient;
                    worksheet.Cells[row, 10].Value = workSalary;
                    worksheet.Cells[row, 11].Value = p.Bonus;
                    worksheet.Cells[row, 12].Value = p.Deductions;
                    worksheet.Cells[row, 13].Value = p.TotalSalary;
                    worksheet.Cells[row, 14].Value = p.Status == "Paid" ? "Đã thanh toán" : "Chưa thanh toán";
                    worksheet.Cells[row, 15].Value = p.Notes;

                    // Border
                    for (int col = 1; col <= 15; col++)
                    {
                        worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }

                    // Alignments
                    worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 10].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 12].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 13].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[row, 14].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // Number formats
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 11].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 12].Style.Numberformat.Format = "#,##0";
                    worksheet.Cells[row, 13].Style.Numberformat.Format = "#,##0";

                    row++;
                }

                // Total row
                worksheet.Cells[row, 1].Value = "Tổng cộng";
                worksheet.Cells[row, 1, row, 4].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Sum Formulas
                if (payrolls.Any())
                {
                    worksheet.Cells[row, 6].Formula = $"SUM(F5:F{row - 1})";
                    worksheet.Cells[row, 7].Formula = $"SUM(G5:G{row - 1})";
                    worksheet.Cells[row, 8].Formula = $"SUM(H5:H{row - 1})";
                    worksheet.Cells[row, 10].Formula = $"SUM(J5:J{row - 1})";
                    worksheet.Cells[row, 11].Formula = $"SUM(K5:K{row - 1})";
                    worksheet.Cells[row, 12].Formula = $"SUM(L5:L{row - 1})";
                    worksheet.Cells[row, 13].Formula = $"SUM(M5:M{row - 1})";
                }
                else
                {
                    worksheet.Cells[row, 6].Value = 0;
                    worksheet.Cells[row, 7].Value = 0;
                    worksheet.Cells[row, 8].Value = 0;
                    worksheet.Cells[row, 10].Value = 0;
                    worksheet.Cells[row, 11].Value = 0;
                    worksheet.Cells[row, 12].Value = 0;
                    worksheet.Cells[row, 13].Value = 0;
                }

                for (int col = 1; col <= 15; col++)
                {
                    var cell = worksheet.Cells[row, col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    if (col >= 6 && col <= 13 && col != 9)
                    {
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        cell.Style.Numberformat.Format = col >= 9 ? "#,##0" : "#,##0.00";
                    }
                }

                // Set heights
                worksheet.Row(1).Height = 40;
                worksheet.Row(4).Height = 25;

                // Auto fit
                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"BangLuong_{month}_{year}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }
}
