using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chamsoc.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string TransactionCode { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string PaymentMethod { get; set; }

        [Required]
        public string Status { get; set; } // Pending, Completed, Failed

        [Required]
        public int CareJobId { get; set; }

        [ForeignKey("CareJobId")]
        public CareJob CareJob { get; set; }

        public int? SeniorId { get; set; }
        [ForeignKey("SeniorId")]
        public Senior? Senior { get; set; }

        public int? CaregiverId { get; set; }
        [ForeignKey("CaregiverId")]
        public Caregiver? Caregiver { get; set; }

        public string? Notes { get; set; }
    }
}