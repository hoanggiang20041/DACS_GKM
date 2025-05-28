using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class HandleComplaintViewModel
    {
        public int ComplaintId { get; set; }
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Resolution { get; set; }
        public string ImagePath { get; set; }
        public string ThumbnailPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CaregiverName { get; set; }
        public string SeniorName { get; set; }
        public Complaint Complaint { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái xử lý")]
        public string NewStatus { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung xử lý")]
        public string NewResolution { get; set; }
    }
} 