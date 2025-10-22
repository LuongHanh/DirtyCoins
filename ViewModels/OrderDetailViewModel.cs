// ViewModels/OrderDetailViewModel.cs
using DirtyCoins.Models;
using System.Collections.Generic;

namespace DirtyCoins.ViewModels
{
    public class OrderDetailViewModel
    {
        public Order Order { get; set; }
        public Customer Customer { get; set; }
        public List<OrderDetailItemVM> OrderDetails { get; set; }
        public decimal PromoDiscount { get; set; }
        public decimal MemberDiscount { get; set; }
        public decimal TotalSavings { get; set; }
    }

    public class OrderDetailItemVM
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }    // 🔹 % giảm giá (nếu có)
        public string PromotionName { get; set; }       // 🔹 tên chương trình KM (nếu có)
        public decimal FinalPrice => UnitPrice * (1 - DiscountPercent / 100);
        public decimal Total => FinalPrice * Quantity;
    }
}
