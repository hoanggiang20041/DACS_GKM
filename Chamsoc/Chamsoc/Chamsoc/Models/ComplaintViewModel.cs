namespace Chamsoc.Models
{
    public class ComplaintViewModel
    {
        public Complaint Complaint { get; set; }
        public CareJob Job { get; set; }
        public Senior Senior { get; set; }
        public Caregiver Caregiver { get; set; }
    }
}