using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Chamsoc.Controllers;
using Chamsoc.Models;

namespace Chamsoc.Services
{
    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public async Task<string> AskAsync(List<ChatMessage> messages)
        {
            try
            {
                var requestBody = new
                {
                    model = "gpt-3.5-turbo", // có thể cấu hình từ appsettings nếu cần
                    messages = messages.Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    }).ToList()
                };

                var response = await _httpClient.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    // Có thể log chi tiết lỗi từ OpenRouter tại đây nếu cần
                    return $"❌ Lỗi khi gọi OpenRouter: {response.StatusCode}";
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>();

                return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                       ?? "❌ Bot không thể phản hồi lúc này. Vui lòng thử lại sau.";
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                return $"❌ Đã xảy ra lỗi khi xử lý yêu cầu: {ex.Message}";
            }
        }

        // Mapping response từ OpenRouter
        public class OpenAiResponse
        {
            public List<Choice> Choices { get; set; }

            public class Choice
            {
                public Message Message { get; set; }
            }

            public class Message
            {
                public string Role { get; set; }
                public string Content { get; set; }
            }
        }
    }
}
