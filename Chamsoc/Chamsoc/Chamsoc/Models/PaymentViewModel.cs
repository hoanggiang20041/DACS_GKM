using System;

namespace Chamsoc.Models
{
    public class PaymentViewModel
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string SeniorName { get; set; }
        public string CaregiverName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Deposit { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDepositPaid { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public string DepositNote { get; set; }
    }

    public class PaymentStatsViewModel
    {
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalDeposits { get; set; }
        public int PaidPayments { get; set; }
        public int PendingPayments { get; set; }
    }
} 