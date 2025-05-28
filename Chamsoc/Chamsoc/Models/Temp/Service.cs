using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CareJob> CareJobs { get; set; } = new List<CareJob>();
}
