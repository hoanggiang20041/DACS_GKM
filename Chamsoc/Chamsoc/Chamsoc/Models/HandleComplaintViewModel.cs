using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class HandleComplaintViewModel
    {
        public int ComplaintId { get; set; }
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public string CaregiverName { get; set; }
        public int SeniorId { get; set; }
        public string SeniorName { get; set; }
        public string Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
        public string Status { get; set; } // "Pending", "Resolved", "Dismissed"

        [StringLength(1000, ErrorMessage = "Giải quyết không được dài quá 1000 ký tự.")]
        public string Resolution { get; set; }
    }
}