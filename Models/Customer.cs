using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Customer
    {
        public int IdCustomer { get; set; }
        [ForeignKey("User")]
        public int IdUser { get; set; }
        public User? User { get; set; }  // ✅

        public string FullName { get; set; } = null!;
        public string? Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Phone { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();       // ✅
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>(); // ✅
    }
}
