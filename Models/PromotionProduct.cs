// Models/PromotionProduct.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class PromotionProduct
    {
        public int IdPromotionProduct { get; set; }

        [ForeignKey("Promotion")]
        public int IdPromotion { get; set; }
        public Promotion Promotion { get; set; }

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }
    }
}
