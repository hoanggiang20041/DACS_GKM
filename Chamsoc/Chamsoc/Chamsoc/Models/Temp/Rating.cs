using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Rating
{
    public int Id { get; set; }

    public int JobId { get; set; }

    public int CaregiverId { get; set; }

    public int SeniorId { get; set; }

    public int Stars { get; set; }

    public string? Comment { get; set; }

    public string? Review { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Caregiver Caregiver { get; set; } = null!;

    public virtual CareJob Job { get; set; } = null!;

    public virtual Senior Senior { get; set; } = null!;
}
