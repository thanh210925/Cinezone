using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CINEMA.Hubs;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    public class SendChatMessageDto
    {
        [Required(ErrorMessage = "Nội dung tin nhắn không được để trống")]
        public string Message { get; set; } = null!;
    }

    [ApiController]
    [Route("api/chat")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileChatApiController : ControllerBase
    {
        private readonly CinemaContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public MobileChatApiController(CinemaContext context, IHubContext<ChatHub> chatHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
        }

        /// <summary>
        /// Lấy lịch sử chat CSKH cho ứng dụng di động Mobile App
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetChatHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var messages = await _context.ChatMessages
                .Include(m => m.Customer)
                .Where(m => m.CustomerId == customerId)
                .OrderBy(m => m.CreatedAt)
                .AsNoTracking()
                .Select(m => new
                {
                    m.ChatMessageId,
                    m.CustomerId,
                    m.IsFromCustomer,
                    SenderName = m.IsFromCustomer ? (m.Customer != null ? m.Customer.FullName : "Khách hàng") : "Hỗ trợ viên Cinezone",
                    MessageText = m.MessageText,
                    m.CreatedAt,
                    m.IsRead
                })
                .ToListAsync();

            return Ok(messages);
        }

        /// <summary>
        /// Gửi tin nhắn chat mới từ Mobile App (Đồng bộ thời gian thực qua SignalR WebSocket)
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendChatMessageDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound(new { message = "Không tìm thấy thông tin khách hàng." });

            var chatMsg = new ChatMessage
            {
                CustomerId = customerId,
                MessageText = model.Message,
                IsFromCustomer = true,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.All.SendAsync("ReceiveMessage", customerId.ToString(), customer.FullName, model.Message, DateTime.Now.ToString("HH:mm dd/MM"));

            return Ok(new
            {
                chatMsg.ChatMessageId,
                chatMsg.CustomerId,
                chatMsg.MessageText,
                chatMsg.CreatedAt,
                chatMsg.IsFromCustomer
            });
        }
    }
}
