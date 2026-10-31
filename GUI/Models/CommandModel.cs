using System;

namespace RWORM.GUI.Models
{
    public class CommandModel
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string Command { get; set; }
        public string Output { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
    }
}