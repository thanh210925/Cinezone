using System.ComponentModel.DataAnnotations;
using CINEMA.Services;
using Microsoft.AspNetCore.Mvc;

namespace CINEMA.Controllers.Api.CustomerApi
{
    public class ChatRequestDto
    {
        [Required(ErrorMessage = "Tin nhắn không được để trống")]
        public string Message { get; set; } = null!;
    }

    [ApiController]
    [Route("api/chatbot")]
    public class ChatbotApiController : ControllerBase
    {
        private readonly GeminiService _geminiService;

        public ChatbotApiController(GeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        /// <summary>
        /// Gửi tin nhắn cho Trợ lý AI tư vấn phim & vé
        /// </summary>
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var rawResult = await _geminiService.Ask(request.Message);
            return Content(rawResult, "application/json");
        }
    }
}
