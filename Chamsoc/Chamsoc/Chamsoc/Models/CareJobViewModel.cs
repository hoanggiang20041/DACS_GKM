using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class CareJobViewModel
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public int CaregiverId { get; set; }
        public string SeniorName { get; set; }
        public string CaregiverName { get; set; }
        public string SeniorPhone { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public decimal TotalBill { get; set; }
        public decimal Deposit { get; set; }
        public decimal RemainingAmount { get; set; }
        public string ServiceType { get; set; }
        public string CreatedByRole { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDepositPaid { get; set; }
        public bool DepositMade { get; set; }
        public bool CaregiverAccepted { get; set; }
        public bool SeniorAccepted { get; set; }
        public string DepositNote { get; set; }
        public List<Notification> Notifications { get; set; }
        public bool HasRated { get; set; }
        public bool HasComplained { get; set; }
        
        // Navigation properties
        public Senior Senior { get; set; }
        public Caregiver Caregiver { get; set; }
    }
} 