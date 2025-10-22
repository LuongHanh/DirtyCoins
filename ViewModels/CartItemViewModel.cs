namespace DirtyCoins.ViewModels
{
    public class CartItemViewModel
    {
        public DirtyCoins.Models.Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal DiscountPercent { get; set; } = 0;

        public decimal DiscountedPrice =>
            Product != null
                ? Math.Round(Product.Price * (1 - DiscountPercent / 100m), 0)
                : 0;

        public decimal SubTotal =>
            Product != null
                ? DiscountedPrice * Quantity
                : 0;
    }
}
