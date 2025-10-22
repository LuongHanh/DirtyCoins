namespace DirtyCoins.ViewModels
{
    public class ProductViewModel
    {
        public int IdProduct { get; set; }
        public string Name { get; set; } = "";
        public string? Image { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public decimal DiscountPercent { get; set; } = 0;
        public decimal DiscountedPrice { get; set; }
        public DirtyCoins.Models.Category? Category { get; set; }
    }
}
