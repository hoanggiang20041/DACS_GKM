using System;
using System.Collections.Generic;

namespace Chamsoc.Models.Temp;

public partial class Notification
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public string Type { get; set; } = null!;

    public string? Link { get; set; }

    public int? JobId { get; set; }

    public string UserId { get; set; } = null!;

    public int? CareJobId { get; set; }

    public virtual CareJob? CareJob { get; set; }

    public virtual CareJob? Job { get; set; }

    public virtual AspNetUser User { get; set; } = null!;
}
