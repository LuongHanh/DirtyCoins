// ViewModels/ProductDetailViewModel.cs
using DirtyCoins.Models;

namespace DirtyCoins.ViewModels
{
    public class ProductDetailViewModel
    {
        public Product Product { get; set; }
        public List<FeedbackViewModel> Feedbacks { get; set; } = new();
        public IEnumerable<Product> RelatedProducts { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountedPrice { get; set; }
        public List<ProductImage> ProductImages => Product?.ProductImages?.ToList() ?? new List<ProductImage>();
    }
}
