namespace Chamsoc.Models
{
    public class ChatMessage
    {
        public string Role { get; set; }      // "system", "user", "assistant"
        public string Content { get; set; }   // Nội dung tin nhắn
    }
} 