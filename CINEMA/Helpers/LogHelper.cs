using CINEMA.Models;
using Microsoft.AspNetCore.Http;

namespace CINEMA.Helpers
{
    public static class LogHelper
    {
        public static void Write(CinemaContext context, IHttpContextAccessor httpContextAccessor, string action, string entity, int entityId)
        {
            // Tự lấy AdminId từ Session
            var adminIdStr = httpContextAccessor.HttpContext?.Session.GetString("AdminId");
            int adminId = int.TryParse(adminIdStr, out int id) ? id : 0;

            var log = new ActivityLog
            {
                AdminId = adminId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                LogDate = System.DateTime.Now
            };

            context.ActivityLogs.Add(log);
            context.SaveChanges();
        }
    }
}