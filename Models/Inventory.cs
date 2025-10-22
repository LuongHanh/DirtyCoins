// Models/Inventory.cs
namespace DirtyCoins.Models
{
    public class Inventory
    {
        public int IdInventory { get; set; }

        public int IdStore { get; set; }
        public Store Store { get; set; }

        public int IdProduct { get; set; }
        public Product Product { get; set; }

        public int Quantity { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}
