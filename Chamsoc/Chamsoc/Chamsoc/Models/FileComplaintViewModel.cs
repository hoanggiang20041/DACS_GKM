using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class FileComplaintViewModel
    {
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public string CaregiverName { get; set; }
        public int SeniorId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung khiếu nại.")]
        [StringLength(1000, ErrorMessage = "Nội dung khiếu nại không được dài quá 1000 ký tự.")]
        public string Description { get; set; }
    }
}