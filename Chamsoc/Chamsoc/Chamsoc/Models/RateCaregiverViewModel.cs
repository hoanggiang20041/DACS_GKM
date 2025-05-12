using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class RateCaregiverViewModel
    {
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public string CaregiverName { get; set; }
        public int SeniorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
        [Range(1, 5, ErrorMessage = "Số sao phải từ 1 đến 5.")]
        public int Stars { get; set; }

        [StringLength(500, ErrorMessage = "Nhận xét không được dài quá 500 ký tự.")]
        public string Review { get; set; }
    }
}