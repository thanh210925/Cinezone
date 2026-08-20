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

        public bool HasValidKey
        {
            get
            {
                var apiKey = _config["GeminiApiKey"];
                return !string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_GEMINI_API_KEY" && apiKey != "apiKey";
            }
        }

        public async Task<string> Ask(string message)
        {
            var apiKey = _config["GeminiApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY" || apiKey == "apiKey")
            {
                // Bỏ qua hoặc cấu hình dự phòng
            }

            var modelsToTry = new[] { "gemini-3.5-flash", "gemini-3-flash-preview", "gemini-2.0-flash" };
            string lastResponse = "";

            foreach (var model in modelsToTry)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = message } } }
                        }
                    };

                    var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, jsonContent);
                    var rawJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return rawJson;
                    }
                    else
                    {
                        lastResponse = rawJson;
                    }
                }
                catch (Exception ex)
                {
                    lastResponse = "{\"error\": {\"message\": \"" + ex.Message + "\"}}";
                }
            }

            return lastResponse;
        }
    }
}