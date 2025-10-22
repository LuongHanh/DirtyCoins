// Models/SystemLog.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class SystemLog
    {
        public int IdLog { get; set; }
        [ForeignKey("User")]
        public int? IdUser { get; set; }
        public User User { get; set; }
        public string Action { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
