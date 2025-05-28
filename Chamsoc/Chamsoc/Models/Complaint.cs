using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class Complaint
    {
        public int Id { get; set; }
        
        [ForeignKey("Job")]
        public int JobId { get; set; }
        
        [ForeignKey("Caregiver")]
        public int CaregiverId { get; set; }
        
        [ForeignKey("Senior")]
        public int SeniorId { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        [Required]
        public string Status { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? Resolution { get; set; } // Cho phép null
        public string? ImagePath { get; set; } // Đường dẫn hình ảnh khiếu nại
        public string? ThumbnailPath { get; set; } // Added for storing thumbnail path

        public virtual CareJob Job { get; set; }
        public virtual Caregiver Caregiver { get; set; }
        public virtual Senior Senior { get; set; }
    }
}