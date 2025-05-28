namespace Chamsoc.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; } // Thêm trường Email
        public bool IsLocked { get; set; } // Thêm trường IsLocked (1: khóa, 0: không khóa)
        public string Role { get; set; }
        public int RoleId { get; set; }
    }
}