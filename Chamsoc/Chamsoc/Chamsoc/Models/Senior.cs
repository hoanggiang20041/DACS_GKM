namespace Chamsoc.Models
{
    public class Senior
    {
        public int Id { get; set; } // Khóa chính, kiểu int
        public string? UserId { get; set; } // Lưu GUID từ AspNetUsers
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? CareNeeds { get; set; }
        public DateTime RegistrationDate { get; set; }
        public bool Status { get; set; }
        public bool IsVerified { get; set; }
        public string? AvatarUrl { get; set; }
        public decimal Price { get; set; }
        public string? IdentityAndHealthDocs { get; set; }
    }
}