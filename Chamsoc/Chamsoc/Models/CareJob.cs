using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class CareJob
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SeniorId { get; set; }

        [ForeignKey("SeniorId")]
        public Senior Senior { get; set; } = null!;

        public int? CaregiverId { get; set; }

        [ForeignKey("CaregiverId")]
        public Caregiver? Caregiver { get; set; }

        [Required]
        public DateTime? StartTime { get; set; }

        [Required]
        public DateTime? EndTime { get; set; }

        [Required]
        [StringLength(100)]
        public string ServiceType { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Đang chờ";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBill { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deposit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAmount { get; set; }

        public bool IsDepositPaid { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; }

        public string CreatedByRole { get; set; }

        public bool DepositMade { get; set; }

        [Required]
        public string DepositNote { get; set; }

        [Required]
public string PaymentStatus { get; set; } = "Chờ thanh toán"; // Trạng thái thanh toán: Chờ thanh toán, Đã thanh toán, Từ chối

        public string PaymentMethod { get; set; }

        public DateTime? PaymentTime { get; set; }

        public DateTime? CompletedAt { get; set; }

        public decimal? Rating { get; set; }

        [StringLength(500)]
        public string? Review { get; set; }

        public int ServiceId { get; set; }

        [ForeignKey("ServiceId")]
        public Service Service { get; set; }

        [Required]
        public string Location { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public bool CaregiverAccepted { get; set; }

        public bool SeniorAccepted { get; set; }

        public string CaregiverName { get; set; }

        public string SeniorName { get; set; }

        public string SeniorPhone { get; set; }

        public bool? HasRated { get; set; }

        public bool? HasComplained { get; set; }

        public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

        public ICollection<Notification> Notifications { get; set; }
    }
}