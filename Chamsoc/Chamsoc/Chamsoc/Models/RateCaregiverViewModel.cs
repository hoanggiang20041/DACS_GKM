using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class RateCaregiverViewModel
    {
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string CaregiverName { get; set; }
        public string Review { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số sao")]
        [Range(1, 5, ErrorMessage = "Số sao phải từ 1 đến 5")]
        public int Stars { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đánh giá")]
        public string Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nhận xét")]
        public string Comment { get; set; }
    }
} 