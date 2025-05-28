using Microsoft.AspNetCore.Mvc;
using Chamsoc.Services;

namespace Chamsoc.Controllers
{
    [Route("Chatbot")]
    public class ChatbotController : Controller
    {
        private readonly OpenRouterService _chatService;

        public ChatbotController(OpenRouterService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("Ask")]
        public async Task<IActionResult> Ask([FromBody] PromptModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Prompt))
                return BadRequest(new { reply = "❌ Vui lòng nhập nội dung câu hỏi." });

            var reply = await _chatService.AskAsync(model.Prompt);
            return Json(new { reply });
        }
    }

    public class PromptModel
    {
        public string Prompt { get; set; }
    }
}
