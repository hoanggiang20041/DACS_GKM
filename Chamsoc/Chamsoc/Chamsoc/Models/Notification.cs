using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int? JobId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        public string Type { get; set; } = "General"; // Default value if not set
    }
}