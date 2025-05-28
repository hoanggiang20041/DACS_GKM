using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Senior
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int Age { get; set; }

    public string CareNeeds { get; set; } = null!;

    public DateTime RegistrationDate { get; set; }

    public bool Status { get; set; }

    public bool IsVerified { get; set; }

    public decimal Price { get; set; }

    public string? IdentityAndHealthDocs { get; set; }

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    public string? FullName { get; set; }

    public virtual ICollection<CareJob> CareJobs { get; set; } = new List<CareJob>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual AspNetUser User { get; set; } = null!;
}
