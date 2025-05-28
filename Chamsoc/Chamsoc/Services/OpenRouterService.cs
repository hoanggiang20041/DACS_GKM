using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

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
        }

        public async Task<string> AskAsync(string prompt)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];
            var apiUrl = _configuration["OpenRouter:ApiUrl"];
            var model = _configuration["OpenRouter:Model"];

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.7
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);


            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new InvalidOperationException("API URL is not configured properly.");
            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"❌ Lỗi: {response.StatusCode} - {responseJson}";
            }

            using var doc = JsonDocument.Parse(responseJson);
            var result = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString();

            return result?.Trim() ?? "❌ Không nhận được phản hồi.";
        }
    }
}
