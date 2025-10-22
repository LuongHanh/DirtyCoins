using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Imported
    {
        [Key]
        public int IdImported { get; set; }

        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }   // Liên kết cửa hàng

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }   // Liên kết sản phẩm

        public int Quantity { get; set; }   // Số lượng nhập

        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}
