namespace Chamsoc.Models
{
    public class CareJob
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public int CaregiverId { get; set; }
        public string? CaregiverName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Status { get; set; }
        public decimal TotalBill { get; set; }
        public decimal Deposit { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? ServiceType { get; set; }
        public int? Rating { get; set; }
        public bool DepositMade { get; set; } = false;
        public string CreatedByRole { get; set; }
        public bool SeniorAccepted { get; set; } = false; // Theo dõi Senior đã chấp nhận chưa
        public bool CaregiverAccepted { get; set; } = false; // Theo dõi Caregiver đã chấp nhận chưa
        public double Latitude { get; set; }  // Đã thêm
        public double Longitude { get; set; } // Đã thêm
    }
}