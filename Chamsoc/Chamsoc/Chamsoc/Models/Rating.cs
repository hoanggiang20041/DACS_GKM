namespace Chamsoc.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int CaregiverId { get; set; }
        public int SeniorId { get; set; }
        public int Stars { get; set; } // 1 to 5 stars
        public string? Review { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}