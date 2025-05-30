using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }
        [ForeignKey("JobId")]
        public CareJob Job { get; set; }

        [Required]
        public int SeniorId { get; set; }
        [ForeignKey("SeniorId")]
        public Senior Senior { get; set; }

        [Required]
        public int CaregiverId { get; set; }
        [ForeignKey("CaregiverId")]
        public Caregiver Caregiver { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Status { get; set; } // Pending, Approved, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }

        public DateTime? RejectedAt { get; set; }
        public string RejectedBy { get; set; }
        public string RejectionReason { get; set; }

        public string PaymentMethod { get; set; } // BankTransfer, Cash, etc.
        public string TransactionId { get; set; } // ID giao dịch từ ngân hàng hoặc bên thứ 3
        public string Notes { get; set; }
    }
} 