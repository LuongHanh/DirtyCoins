using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class MonthlyInventory
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }

        public int Month { get; set; }
        public int Year { get; set; }

        public int BeginQty { get; set; }       // Số lượng tồn đầu kỳ
        public int ImportedQty { get; set; }    // Số lượng nhập trong kỳ
        public int SoldQty { get; set; }        // Số lượng bán trong kỳ
        public int EndQty { get; set; }         // Số lượng tồn cuối kỳ

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
