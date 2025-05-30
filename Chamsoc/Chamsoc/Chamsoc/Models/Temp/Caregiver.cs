using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Caregiver
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Skills { get; set; } = null!;

    public bool IsAvailable { get; set; }

    public string? Certificate { get; set; }

    public string? IdentityAndHealthDocs { get; set; }

    public DateTime RegistrationDate { get; set; }

    public bool IsVerified { get; set; }

    public decimal Rating { get; set; }

    public int CompletedJobs { get; set; }

    public decimal HourlyRate { get; set; }

    public string Experience { get; set; } = null!;

    public string Pricing { get; set; } = null!;

    public double TotalRatings { get; set; }

    public string? Name { get; set; }

    public string? Contact { get; set; }

    public string? AvatarUrl { get; set; }

    public string? CertificateFilePath { get; set; }

    public decimal Price { get; set; }

    public string? FullName { get; set; }

    public virtual ICollection<CareJob> CareJobs { get; set; } = new List<CareJob>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual AspNetUser User { get; set; } = null!;
}
