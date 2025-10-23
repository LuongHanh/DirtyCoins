// Models/Product.cs
using DocumentFormat.OpenXml.Wordprocessing;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Product
    {
        public int IdProduct { get; set; }
        public string Name { get; set; }
        [ForeignKey("Category")]
        public int? IdCategory { get; set; }
        public Category? Category { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; } // global stock (optional)
        public string Image { get; set; }

        [ForeignKey("Store")]
        public int? IdStore { get; set; }
        public Store? Store { get; set; }
        public string Intro { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; }
        public ICollection<Feedback> Feedbacks { get; set; }
        public ICollection<ProductImage> ProductImages { get; set; }
    }
}
