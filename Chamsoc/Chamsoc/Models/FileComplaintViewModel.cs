using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class FileComplaintViewModel
    {
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string CaregiverName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả khiếu nại")]
        public string Description { get; set; }

        public IFormFile? ImageFile { get; set; }
    }
} 