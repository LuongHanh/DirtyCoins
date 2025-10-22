// Models/User.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class CustomerRankStat
    {
        [Key]
        public int Id { get; set; }
        public int IdCustomer { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
        public string RankName { get; set; }
        public int DiscountPercent { get; set; }
        public DateTime CreatedAt { get; set; }

        [ForeignKey("IdCustomer")]
        public Customer Customer { get; set; }
    }
}
