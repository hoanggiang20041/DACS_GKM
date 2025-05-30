using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class BookSeniorViewModel
    {
        public int SeniorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại dịch vụ")]
        public string ServiceType { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời lượng")]
        public int Duration { get; set; }

        // Senior information
        public string SeniorName { get; set; }
        public string SeniorAvatar { get; set; }
        public string SeniorLocation { get; set; }
        public string SeniorPhone { get; set; }
        public string HealthInfo { get; set; }
        public decimal Price { get; set; }
    }
} 