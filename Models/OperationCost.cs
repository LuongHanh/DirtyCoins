using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class OperationCost
    {
        [Key]
        public int IdCost { get; set; }

        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }   // liên kết với cửa hàng

        [Required, MaxLength(100)]
        public string CostType { get; set; }   // Loại chi phí: điện, nước, lương...

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostAmount { get; set; }   // Số tiền

        [Range(1, 12)]
        public int CostMonth { get; set; }   // Tháng

        public int CostYear { get; set; }   // Năm

        [MaxLength(255)]
        public string? Note { get; set; }   // Ghi chú
    }
}
