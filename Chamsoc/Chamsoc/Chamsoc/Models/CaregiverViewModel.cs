using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class CaregiverViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nghề nghiệp")]
        public string Skills { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số")]
        public string Contact { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá mong muốn")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Vui lòng tải lên bằng cấp")]
        public string Degree { get; set; }

        public bool IsAvailable { get; set; }
        public bool IsVerified { get; set; }
        public string AvatarUrl { get; set; }
        public string CertificateFilePath { get; set; }

        // Thêm các thuộc tính liên quan đến đánh giá
        public double AverageRating { get; set; }
        public int RatingCount { get; set; }
        public List<Rating> RecentRatings { get; set; }
        public Caregiver Caregiver { get; set; }
    }
} 