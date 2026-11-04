using System;

namespace RWORM.GUI.Models
{
    public class CommandModel
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Command { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }
}
