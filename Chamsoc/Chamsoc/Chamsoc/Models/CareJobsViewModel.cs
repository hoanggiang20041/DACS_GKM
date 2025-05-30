using System.Collections.Generic;

namespace Chamsoc.Models
{
    public class CareJobsViewModel
    {
        public List<CareJob> Jobs { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public MonthlyStats MonthlyStats { get; set; }
    }

    public class MonthlyStats
    {
        public int TotalJobs { get; set; }
        public int CompletedJobs { get; set; }
        public decimal TotalEarnings { get; set; }
    }
} 