namespace Chamsoc.Models
{
    public class TransactionDetailViewModel
    {
        public int Id { get; set; }
        public string SeniorName { get; set; }
        public string SeniorEmail { get; set; }
        public string SeniorPhone { get; set; }
        public string CaregiverName { get; set; }
        public string CaregiverEmail { get; set; }
        public string CaregiverPhone { get; set; }
        public string ServiceName { get; set; }
        public decimal TotalBill { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; }
        public DateTime TransactionDateTime { get; set; }
    }
} 