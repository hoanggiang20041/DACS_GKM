using System.ComponentModel.DataAnnotations;

namespace Chamsoc.Models
{
    public class ComplaintViewModel
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string? ImagePath { get; set; }
        public string? Resolution { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string CaregiverName { get; set; }
        public string SeniorName { get; set; }
        public Complaint Complaint { get; set; }
        public CareJob Job { get; set; }
        public Caregiver Caregiver { get; set; }
        public Senior Senior { get; set; }
    }
} 