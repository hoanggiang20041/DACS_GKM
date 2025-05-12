namespace Chamsoc.Models
{
    public class Caregiver
    {
        public int Id { get; set; } // Khóa chính, kiểu int
        public string? UserId { get; set; } // Lưu GUID từ AspNetUsers
        public string? Name { get; set; }
        public string? Skills { get; set; }
        public string? Contact { get; set; }
        public bool IsAvailable { get; set; }
        public string? CertificateFilePath { get; set; }
        public bool IsVerified { get; set; }
        public string? AvatarUrl { get; set; }
        public decimal Price { get; set; }
        public string? Pricing { get; set; }

    }
}