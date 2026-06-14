using System.Text;
using Newtonsoft.Json;

namespace CINEMA.Services // Đổi namespace cho khớp với dự án của bạn nếu cần
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public GeminiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> Ask(string message)
        {
            try
            {
                var apiKey = _config["GeminiApiKey"];

                // Dùng model gemini-1.5-flash cho tốc độ phản hồi nhanh nhất
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={"keyAPI"}";
                // Tạo cấu trúc dữ liệu JSON đúng chuẩn mà Google yêu cầu
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = message } } }
                    }
                };

                var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                // Gửi request lên Google
                var response = await _httpClient.PostAsync(url, jsonContent);
                var rawJson = await response.Content.ReadAsStringAsync();

                return rawJson;
            }
            catch (Exception ex)
            {
                // In ra lỗi trên console để dễ debug
                Console.WriteLine($"Lỗi gọi Gemini API: {ex.Message}");
                return "{}"; // Trả về chuỗi rỗng để controller tự bắt lỗi
            }
        }
    }
}