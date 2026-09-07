using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;

namespace CINEMA.Controllers
{
    public class AdminBaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var adminId = HttpContext.Session.GetString("AdminId");
            var role = HttpContext.Session.GetString("Role");

            // 1. Kiểm tra xem đã đăng nhập chưa bằng AdminId
            if (string.IsNullOrEmpty(adminId))
            {
                context.Result = RedirectToAction("Login", "Admin");
                return;
            }

            // 2. Phân quyền truy cập theo Role (SuperAdmin có toàn quyền)
            if (!string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                string controllerName = context.RouteData.Values["Controller"]?.ToString() ?? "";

                // Các controller dùng chung cho tất cả các tài khoản Admin/Staff đã đăng nhập
                var commonControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Admin",            // Dashboard, Profile...
                    "AdminAttendance",  // MySchedule, CheckInOut
                    "AdminLeave"        // LeaveRequest
                };

                bool isAllowed = commonControllers.Contains(controllerName);

                if (!isAllowed)
                {
                    if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
                    {
                        // Manager: Phim, Lịch chiếu, Rạp, Phòng chiếu, Thể loại, Combo, Chatbot hỗ trợ, Báo cáo thống kê
                        var managerControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Theater", "Auditorium", "Movie", "Showtime", "Genre", "Combo",
                            "AdminChat", "Statistics"
                        };
                        isAllowed = managerControllers.Contains(controllerName);
                    }
                    else if (string.Equals(role, "CRM", StringComparison.OrdinalIgnoreCase))
                    {
                        // CRM: Quản lý KH, Đánh giá, Đơn hàng, Voucher, Popup, Tracking, Chatbot CSKH, Báo cáo
                        var crmControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "CustomerAdmin", "Reviews", "AdminOrders", "AdminVoucher",
                            "AdminVoucherCondition", "AdminPopup", "AdminTracking", "AdminChat", "Statistics"
                        };
                        isAllowed = crmControllers.Contains(controllerName);
                    }
                    else if (string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
                    {
                        // Staff: Xem Phim, Xem Lịch chiếu, Quản lý Đơn hàng (bán vé & quét mã QR)
                        var staffControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Movie", "Showtime", "AdminOrders"
                        };
                        isAllowed = staffControllers.Contains(controllerName);
                    }
                }

                // Nếu không nằm trong danh sách được phép
                if (!isAllowed)
                {
                    context.Result = new RedirectToActionResult("Dashboard", "Admin", new { error = "Unauthorized" });
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}