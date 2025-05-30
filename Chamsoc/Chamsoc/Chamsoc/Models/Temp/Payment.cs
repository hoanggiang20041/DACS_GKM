using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Payment
{
    public int Id { get; set; }

    public int JobId { get; set; }

    public int SeniorId { get; set; }

    public int CaregiverId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string ApprovedBy { get; set; } = null!;

    public DateTime? RejectedAt { get; set; }

    public string RejectedBy { get; set; } = null!;

    public string RejectionReason { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string TransactionId { get; set; } = null!;

    public string Notes { get; set; } = null!;

    public virtual Caregiver Caregiver { get; set; } = null!;

    public virtual CareJob Job { get; set; } = null!;

    public virtual Senior Senior { get; set; } = null!;
}
