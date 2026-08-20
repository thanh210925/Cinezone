using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class AdminChatController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminChatController(CinemaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult ChatSupport()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId")))
            {
                return RedirectToAction("Login", "Admin");
            }
            return View("~/Views/Admin/ChatSupport.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveChats()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId")))
            {
                return Unauthorized();
            }

            var activeChats = await _context.ChatMessages
                .GroupBy(m => m.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    CustomerName = g.FirstOrDefault() != null && g.FirstOrDefault()!.Customer != null ? g.FirstOrDefault()!.Customer.FullName : "Khách hàng",
                    AvatarUrl = g.FirstOrDefault() != null && g.FirstOrDefault()!.Customer != null ? (g.FirstOrDefault()!.Customer.Avatar ?? g.FirstOrDefault()!.Customer.AvatarUrl ?? "/images/default-avatar.png") : "/images/default-avatar.png",
                    LastMessage = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault() != null ? g.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.MessageText : "",
                    LastMessageTime = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault() != null ? g.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.CreatedAt : DateTime.Now,
                    UnreadCount = g.Count(m => m.IsFromCustomer && !m.IsRead)
                })
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            var result = activeChats.Select(c => new
            {
                c.CustomerId,
                c.CustomerName,
                c.AvatarUrl,
                c.LastMessage,
                LastMessageTime = c.LastMessageTime.ToString("HH:mm dd/MM"),
                c.UnreadCount
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetChatHistoryAdmin(int customerId)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminId")))
            {
                return Unauthorized();
            }

            var unreadMessages = _context.ChatMessages
                .Where(m => m.CustomerId == customerId && m.IsFromCustomer && !m.IsRead)
                .ToList();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            var messages = await _context.ChatMessages
                .Where(m => m.CustomerId == customerId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    type = m.IsFromCustomer ? "customer" : "admin",
                    senderName = m.IsFromCustomer ? (m.Customer != null ? m.Customer.FullName : "Khách hàng") : "Nhân viên hỗ trợ",
                    text = m.MessageText,
                    time = m.CreatedAt.ToString("HH:mm dd/MM")
                })
                .ToListAsync();

            return Json(new { success = true, messages = messages });
        }
    }
}
