using System;

namespace Chamsoc.Core.Models
{
    public class Log
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; }
    }
}