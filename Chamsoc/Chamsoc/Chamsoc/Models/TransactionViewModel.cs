using System;

namespace Chamsoc.Models
{
    public class TransactionViewModel
    {
        public int Id { get; set; }
        public string SeniorName { get; set; }
        public string CaregiverName { get; set; }
        public string ServiceType { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
    }

    public class TransactionStatsViewModel
    {
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public int CompletedTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int CancelledTransactions { get; set; }
    }
} 