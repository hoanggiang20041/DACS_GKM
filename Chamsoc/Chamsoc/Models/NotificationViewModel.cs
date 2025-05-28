namespace Chamsoc.Models
{
    public class NotificationViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string Type { get; set; }
        public string? Link { get; set; }
        public Notification Notification { get; set; }
        public CareJob Job { get; set; }
        public Senior Senior { get; set; }
    }
} 