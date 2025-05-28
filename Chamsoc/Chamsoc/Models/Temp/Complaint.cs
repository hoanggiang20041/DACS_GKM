using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Complaint
{
    public int Id { get; set; }

    public int JobId { get; set; }

    public int CaregiverId { get; set; }

    public int SeniorId { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? Resolution { get; set; }

    public string? ImagePath { get; set; }

    public string? ThumbnailPath { get; set; }

    public virtual Caregiver Caregiver { get; set; } = null!;

    public virtual CareJob Job { get; set; } = null!;

    public virtual Senior Senior { get; set; } = null!;
}
