using Microsoft.AspNetCore.Identity;

namespace Chamsoc.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string RoleId { get; set; }
        public string FullName { get; set; }
        public string Address { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string? Avatar { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Role { get; set; }
        public bool IsLocked { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}