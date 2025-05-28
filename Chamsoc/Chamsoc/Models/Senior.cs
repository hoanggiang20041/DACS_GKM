using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class Senior
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int Age { get; set; }

        [Required]
        [StringLength(500)]
        public string CareNeeds { get; set; } = null!;

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        
        public bool Status { get; set; } = true;
        
        public bool IsVerified { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? IdentityAndHealthDocs { get; set; }

        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public string? FullName { get; set; }

        public ICollection<CareJob> CareJobs { get; set; } = new List<CareJob>();
    }
}