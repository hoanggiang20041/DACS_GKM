using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Chamsoc.Models;

namespace Chamsoc.Services
{
    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenRouterService> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Lấy API key từ configuration
            _apiKey = _configuration["OpenRouter:ApiKey"];
            _apiUrl = _configuration["OpenRouter:ApiUrl"];

            // Kiểm tra API key
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("OpenRouter API key is missing in configuration!");
                throw new InvalidOperationException("OpenRouter API key is not configured");
            }

            _logger.LogInformation("Initializing OpenRouterService with API key: {ApiKeyPrefix}...", _apiKey.Substring(0, 10));

            // Cấu hình HttpClient
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _configuration["OpenRouter:Referer"] ?? "https://localhost:7198");
            _httpClient.DefaultRequestHeaders.Add("X-Title", _configuration["OpenRouter:Title"] ?? "GKM Care");

            // Log headers for debugging
            _logger.LogInformation("HTTP Headers configured: {Headers}", 
                string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
        }

        public async Task<string> AskAsync(List<ChatMessage> messages)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogError("OpenRouter API key is missing!");
                    return "❌ Lỗi cấu hình: API key không tồn tại";
                }

                // Validate messages
                if (messages == null || !messages.Any())
                {
                    _logger.LogError("Messages list is null or empty");
                    return "❌ Lỗi: Không có tin nhắn để gửi";
                }

                // Add system message if not present
                if (!messages.Any(m => m.Role == "system"))
                {
                    messages.Insert(0, new ChatMessage
                    {
                        Role = "system",
                        Content = "Bạn là một trợ lý chăm sóc sức khỏe thông minh, nhiệt tình và hữu ích. Hãy trả lời các câu hỏi về sức khỏe một cách chính xác và dễ hiểu."
                    });
                }

                var request = new
                {
                    model = _configuration["OpenRouter:Model"] ?? "openai/gpt-3.5-turbo",
                    messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                    temperature = 0.7,
                    max_tokens = 1000,
                    stream = false
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                _logger.LogInformation("Sending request to OpenRouter: {Request}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_apiUrl, content);

                _logger.LogInformation("OpenRouter response status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenRouter API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError("Unauthorized error. API Key: {ApiKeyPrefix}...", _apiKey.Substring(0, 10));
                        return "❌ Lỗi xác thực: API key không hợp lệ hoặc hết hạn. Vui lòng kiểm tra lại API key trong appsettings.json";
                    }
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        _logger.LogError("Internal Server Error from OpenRouter. Request: {Request}", json);
                        return "❌ Lỗi máy chủ: Vui lòng thử lại sau hoặc liên hệ hỗ trợ kỹ thuật";
                    }
                    
                    return $"❌ Lỗi khi gọi OpenRouter: {response.StatusCode} - {errorContent}";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("OpenRouter response: {Response}", responseContent);

                var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (responseObj?.Choices == null || !responseObj.Choices.Any())
                {
                    _logger.LogWarning("No response content from OpenRouter");
                    return "❌ Không nhận được phản hồi từ OpenRouter";
                }

                return responseObj.Choices[0].Message.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AskAsync");
                return $"❌ Lỗi: {ex.Message}";
            }
        }
    }

    public class OpenRouterResponse
    {
        public List<Choice> Choices { get; set; }
        public string Error { get; set; }
    }

    public class Choice
    {
        public Message Message { get; set; }
    }

    public class Message
    {
        public string Content { get; set; }
    }
}
