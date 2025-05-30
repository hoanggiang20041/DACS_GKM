using Microsoft.AspNetCore.Mvc;
using Chamsoc.Services;
using Chamsoc.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Ask([FromBody] ChatRequest model)
        {
            if (model.Messages == null || !model.Messages.Any())
                return BadRequest(new { reply = "❌ Vui lòng nhập nội dung câu hỏi." });

            // Lấy nội dung câu hỏi cuối cùng từ người dùng
            var userMessage = model.Messages.Last().Content.ToLower();

            // Danh sách từ khóa y tế
            var medicalKeywords = new[]
            {
                "bệnh", "triệu chứng", "sức khỏe", "đau", "thuốc", "sốt", "khám", "điều trị",
                "vaccine", "bác sĩ", "huyết áp", "đường huyết", "tiêm", "mụn", "da liễu",
                "tim mạch", "hô hấp", "xét nghiệm", "dị ứng", "chẩn đoán", "uống thuốc", "virus", "ung thư"
            };

            bool isMedicalQuestion = medicalKeywords.Any(keyword => userMessage.Contains(keyword));

            // Nếu không phải câu hỏi y tế, trả lời nhẹ nhàng và đồng cảm
            if (!isMedicalQuestion)
            {
                return Json(new
                {
                    reply = "🌼 Mình là trợ lý chăm sóc sức khỏe, hiện tại mình chỉ có thể hỗ trợ các câu hỏi liên quan đến y tế thôi. Nhưng mình rất sẵn lòng lắng nghe nếu bạn đang cần chia sẻ. Bạn thử hỏi mình về sức khỏe, triệu chứng hoặc cách chăm sóc nhé!"
                });
            }

            // System Prompt thân thiện và đồng cảm
            model.Messages.Insert(0, new ChatMessage
            {
                Role = "system",
                Content = "Bạn là một trợ lý chăm sóc sức khỏe thân thiện, đồng cảm và biết lắng nghe. Hãy chỉ trả lời các câu hỏi liên quan đến y tế. Nếu người dùng nói ngoài phạm vi đó, hãy từ chối nhẹ nhàng, khuyến khích họ chia sẻ vấn đề sức khỏe. Nếu người dùng có tâm sự, hãy an ủi họ bằng lời nói ấm áp và khích lệ tinh thần."
            });

            var reply = await _chatService.AskAsync(model.Messages);
            return Json(new { reply });
        }
    }

    // Danh sách tin nhắn truyền vào
    public class ChatRequest
    {
        public List<ChatMessage> Messages { get; set; }
    }
}
