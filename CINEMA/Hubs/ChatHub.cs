using CINEMA.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace CINEMA.Hubs
{
    public class ChatHub : Hub
    {
        private readonly CinemaContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatHub(CinemaContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Khách hàng gửi tin nhắn cho Nhân viên/Admin
        public async Task SendMessageFromCustomer(string messageText)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var customerId = httpContext?.Session.GetInt32("CustomerId");
            var customerName = httpContext?.Session.GetString("CustomerName") ?? "Khách hàng";

            if (customerId == null)
            {
                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Vui lòng đăng nhập để trò chuyện với nhân viên.");
                return;
            }

            // Kiểm tra xem đây có phải tin nhắn đầu tiên của khách hàng hay không
            bool isFirstMessage = !_context.ChatMessages.Any(m => m.CustomerId == customerId.Value);

            // Lưu tin nhắn vào Database
            var chatMsg = new ChatMessage
            {
                CustomerId = customerId.Value,
                MessageText = messageText.Trim(),
                IsFromCustomer = true,
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            // Gửi tin nhắn đến tất cả Admin trong group "Admins"
            await Clients.Group("Admins").SendAsync("ReceiveMessageFromCustomer", customerId.Value, customerName, messageText.Trim(), chatMsg.CreatedAt.ToString("HH:mm dd/MM"), isFirstMessage);

            // Gửi lại chính khách hàng (Caller) để hiển thị lên UI
            await Clients.Caller.SendAsync("ReceiveMessage", "user", messageText.Trim(), chatMsg.CreatedAt.ToString("HH:mm dd/MM"));
        }

        // Admin/Nhân viên gửi tin nhắn phản hồi cho Khách hàng
        public async Task SendMessageFromAdmin(int customerId, string messageText)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var adminId = httpContext?.Session.GetString("AdminId");
            var adminName = httpContext?.Session.GetString("Name") ?? "Nhân viên";

            if (adminId == null)
            {
                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Phiên làm việc hết hạn. Vui lòng đăng nhập lại.");
                return;
            }

            // Lưu tin nhắn phản hồi vào Database
            var chatMsg = new ChatMessage
            {
                CustomerId = customerId,
                MessageText = messageText.Trim(),
                IsFromCustomer = false,
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            // Gửi tin nhắn phản hồi tới khách hàng cụ thể
            await Clients.Group($"Customer_{customerId}").SendAsync("ReceiveMessageFromAdmin", adminName, messageText.Trim(), chatMsg.CreatedAt.ToString("HH:mm dd/MM"));

            // Đồng bộ tin nhắn này tới các Admin khác đang theo dõi cùng cuộc trò chuyện
            await Clients.Group("Admins").SendAsync("ReceiveMessageFromAdminToCustomer", customerId, adminName, messageText.Trim(), chatMsg.CreatedAt.ToString("HH:mm dd/MM"));
        }

        // Khi người dùng kết nối, phân loại Group dựa trên Session
        public override async Task OnConnectedAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var customerId = httpContext?.Session.GetInt32("CustomerId");
            var adminId = httpContext?.Session.GetString("AdminId");

            if (adminId != null)
            {
                // Thêm Admin vào group Admins
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }

            if (customerId != null)
            {
                // Thêm Khách hàng vào group riêng
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Customer_{customerId}");
            }

            await base.OnConnectedAsync();
        }
    }
}
