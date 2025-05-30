using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class Rating
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }

        [Required]
        public int CaregiverId { get; set; }

        [Required]
        public int SeniorId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Stars { get; set; }

        [StringLength(500)]
        public string? Comment { get; set; }

        [StringLength(500)]
        public string? Review { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("JobId")]
        public CareJob Job { get; set; }

        [ForeignKey("CaregiverId")]
        public Caregiver Caregiver { get; set; }

        [ForeignKey("SeniorId")]
        public Senior Senior { get; set; }
    }
}