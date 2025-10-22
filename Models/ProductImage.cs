using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; } // hoặc path lưu trên server

        [ForeignKey("Product")]
        public int IdProduct { get; set; }
        public Product Product { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
