// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace DirtyCoins.Models
{
    public class MaintenanceInfo
    {
        public bool IsMaintenance { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Reason { get; set; }
    }
}
