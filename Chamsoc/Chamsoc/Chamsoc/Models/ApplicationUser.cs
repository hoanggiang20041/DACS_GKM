using Microsoft.AspNetCore.Identity;

namespace Chamsoc.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Role { get; set; }
        public string? RoleId { get; set; }
        public bool IsLocked { get; set; }
        public decimal Balance { get; set; } // Số dư của Admin
    }
}