using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class CareJob
{
    public int Id { get; set; }

    public int SeniorId { get; set; }

    public int? CaregiverId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string ServiceType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal TotalBill { get; set; }

    public decimal DepositAmount { get; set; }

    public bool IsDepositPaid { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public decimal? Rating { get; set; }

    public string? Review { get; set; }

    public int ServiceId { get; set; }

    public string Location { get; set; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public bool CaregiverAccepted { get; set; }

    public bool SeniorAccepted { get; set; }

    public string CreatedByRole { get; set; } = null!;

    public decimal Deposit { get; set; }

    public bool DepositMade { get; set; }

    public string DepositNote { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public DateTime? PaymentTime { get; set; }

    public decimal RemainingAmount { get; set; }

    public string CaregiverName { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string SeniorName { get; set; } = null!;

    public string SeniorPhone { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public bool? HasComplained { get; set; }

    public bool? HasRated { get; set; }

    public virtual Caregiver? Caregiver { get; set; }

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Notification> NotificationCareJobs { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationJobs { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual Senior Senior { get; set; } = null!;

    public virtual Service Service { get; set; } = null!;
}
