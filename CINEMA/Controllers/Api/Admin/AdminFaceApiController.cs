using System.Security.Claims;
using System.Text.Json;
using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    [ApiController]
    [Route("api/admin/face")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,Manager,Staff")]
    public class AdminFaceApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminFaceApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Đăng ký vector nhận diện khuôn mặt (128D Face Embedding) cho Admin/Nhân viên
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceDto model)
        {
            if (!ModelState.IsValid || model.FaceDescriptor == null || model.FaceDescriptor.Count == 0)
                return BadRequest(new { message = "Dữ liệu vector khuôn mặt không hợp lệ." });

            int targetAdminId = 0;
            if (model.AdminId.HasValue && model.AdminId.Value > 0)
            {
                targetAdminId = model.AdminId.Value;
            }
            else
            {
                var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out targetAdminId))
                    return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var admin = await _context.Admins.FindAsync(targetAdminId);
            if (admin == null)
                return NotFound(new { message = "Không tìm thấy thông tin nhân viên." });

            admin.FaceDescriptor = JsonSerializer.Serialize(model.FaceDescriptor);
            if (!string.IsNullOrEmpty(model.FacePhotoUrl))
            {
                admin.FacePhoto = model.FacePhotoUrl;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã đăng ký dữ liệu khuôn mặt thành công cho nhân viên {admin.FullName}." });
        }

        /// <summary>
        /// Điểm danh bằng nhận diện khuôn mặt (Face Check-in API)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("checkin")]
        public async Task<IActionResult> FaceCheckin([FromBody] FaceCheckinDto model)
        {
            if (!ModelState.IsValid || model.FaceDescriptor == null || model.FaceDescriptor.Count == 0)
                return BadRequest(new { message = "Dữ liệu vector khuôn mặt chụp từ camera không hợp lệ." });

            var adminsWithFace = await _context.Admins
                .Where(a => a.IsActive && !string.IsNullOrEmpty(a.FaceDescriptor))
                .AsNoTracking()
                .ToListAsync();

            if (!adminsWithFace.Any())
            {
                return Ok(new FaceCheckinResultDto
                {
                    IsMatched = false,
                    Message = "Chưa có nhân viên nào đăng ký dữ liệu khuôn mặt trên hệ thống."
                });
            }

            Admin? bestMatchAdmin = null;
            double minDistance = double.MaxValue;

            foreach (var admin in adminsWithFace)
            {
                try
                {
                    var storedVector = JsonSerializer.Deserialize<List<float>>(admin.FaceDescriptor!);
                    if (storedVector == null || storedVector.Count != model.FaceDescriptor.Count)
                        continue;

                    double sumSq = 0;
                    for (int i = 0; i < model.FaceDescriptor.Count; i++)
                    {
                        double diff = model.FaceDescriptor[i] - storedVector[i];
                        sumSq += diff * diff;
                    }
                    double distance = Math.Sqrt(sumSq);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestMatchAdmin = admin;
                    }
                }
                catch
                {
                }
            }

            const double threshold = 0.6;
            if (bestMatchAdmin != null && minDistance <= threshold)
            {
                double confidence = Math.Round((1.0 - (minDistance / threshold)) * 100.0, 1);
                confidence = Math.Clamp(confidence, 60.0, 99.9);

                var today = DateTime.Today;
                var now = DateTime.Now;

                var attendance = await _context.Attendance
                    .FirstOrDefaultAsync(a => a.AdminId == bestMatchAdmin.AdminId && a.Date == today);

                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        AdminId = bestMatchAdmin.AdminId ?? 0,
                        Date = today,
                        CheckInTime = now,
                        IsApproved = true
                    };
                    _context.Attendance.Add(attendance);
                }
                else if (attendance.CheckOutTime == null)
                {
                    attendance.CheckOutTime = now;
                }

                await _context.SaveChangesAsync();

                return Ok(new FaceCheckinResultDto
                {
                    IsMatched = true,
                    AdminId = bestMatchAdmin.AdminId,
                    FullName = bestMatchAdmin.FullName,
                    EmployeeCode = bestMatchAdmin.EmployeeCode,
                    ConfidencePercent = confidence,
                    CheckInTime = now,
                    AttendanceId = attendance.AttendanceId,
                    Message = $"Xác nhận điểm danh thành công! Xin chào {bestMatchAdmin.FullName} (Độ tin cậy: {confidence}%)."
                });
            }

            return Ok(new FaceCheckinResultDto
            {
                IsMatched = false,
                ConfidencePercent = 0,
                CheckInTime = DateTime.Now,
                Message = "Khuôn mặt không trùng khớp với bất kỳ nhân viên nào trong hệ thống."
            });
        }

        /// <summary>
        /// Lấy lịch sử điểm danh nhân viên
        /// </summary>
        [HttpGet("attendance-history")]
        public async Task<IActionResult> GetAttendanceHistory([FromQuery] DateTime? date = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Attendance
                .Include(a => a.Admin)
                .AsNoTracking();

            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                query = query.Where(a => a.Date == targetDate);
            }

            var totalItems = await query.CountAsync();
            var list = await query
                .OrderByDescending(a => a.CheckInTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.AttendanceId,
                    a.AdminId,
                    AdminName = a.Admin != null ? a.Admin.FullName : "Nhân viên",
                    EmployeeCode = a.Admin != null ? a.Admin.EmployeeCode : "",
                    a.Date,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.IsApproved
                })
                .ToListAsync();

            return Ok(new { totalItems, page, pageSize, data = list });
        }
    }
}
