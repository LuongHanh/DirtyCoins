// Models/OrderDetail.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class OrderDetail
    {
        public int IdDetail { get; set; }
        [ForeignKey("Order")]
        public int IdOrder { get; set; }
        public Order Order { get; set; }

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
