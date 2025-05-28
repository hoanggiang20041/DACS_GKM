using System;

namespace Chamsoc.Models
{
    public class BalanceHistoryViewModel
    {
        public DateTime CreatedAt { get; set; }
        public int PaymentId { get; set; }
        public string Type { get; set; } // "Received" or "Sent"
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public decimal BalanceAfter { get; set; }
    }
} 