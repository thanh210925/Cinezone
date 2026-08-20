using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CINEMA.Controllers
{
    public class AdminBaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // 1. Kiểm tra xem đã đăng nhập chưa bằng AdminId
            var adminId = HttpContext.Session.GetString("AdminId");
            var role = HttpContext.Session.GetString("Role");

            // 2. Nếu không có AdminId (chưa đăng nhập), chuyển về Login
            if (string.IsNullOrEmpty(adminId))
            {
                context.Result = RedirectToAction("Login", "Admin");
                return;
            }

            // 3. Nếu đã đăng nhập, cho phép đi tiếp (Dù là SuperAdmin hay Staff)
            // Không nên chặn ở đây nếu không có yêu cầu cụ thể về Role

            base.OnActionExecuting(context);
        }
    }
}