// Models/Report.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Report
    {
        public int IdReport { get; set; }
        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }

        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
    }
}
