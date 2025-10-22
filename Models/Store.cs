// Models/Store.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Store
    {
        public int IdStore { get; set; }
        public string StoreName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Email { get; set; }
        public string StoreOwner { get; set; }
        [ForeignKey("User")]
        public int IdUser { get; set; }
        public User? User { get; set; }  // ✅

        public ICollection<Employee> Employees { get; set; }
        public ICollection<Inventory> Inventories { get; set; }
        public ICollection<Report> Reports { get; set; }
        public ICollection<MonthlyInventory> MonthlyInventories { get; set; }
    }
}
