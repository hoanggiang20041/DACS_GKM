namespace Chamsoc.Models
{
    public class Complaint
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Resolution { get; set; } // Cho phép null

        public CareJob Job { get; set; }
        public Caregiver Caregiver { get; set; }
        public Senior Senior { get; set; }
    }
}