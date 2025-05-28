using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class BookCaregiverViewModel
    {
        public int CareJobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string CaregiverName { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại dịch vụ")]
        public string ServiceType { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số giờ")]
        [Range(1, 24, ErrorMessage = "Số giờ phải từ 1 đến 24")]
        public int NumberOfHours { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa điểm")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả công việc")]
        public string Description { get; set; }

        public int ServiceId { get; set; }
        public decimal ServicePrice { get; set; }

        public decimal TotalBill => ServicePrice * NumberOfHours;
        public decimal Deposit => TotalBill * 0.3m;
        public decimal RemainingAmount => TotalBill - Deposit;

        // Caregiver information
        public string CaregiverAvatar { get; set; }
        public string CaregiverLocation { get; set; }
        public string CaregiverSkills { get; set; }
        public double CaregiverRating { get; set; }
        public int CaregiverRatingCount { get; set; }
    }
} 