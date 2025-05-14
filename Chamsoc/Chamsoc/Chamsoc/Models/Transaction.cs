using System;
using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public string TransactionCode { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public int CareJobId { get; set; }
        public CareJob CareJob { get; set; }
    }
}