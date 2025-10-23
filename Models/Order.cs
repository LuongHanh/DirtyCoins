// Models/Order.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirtyCoins.Models
{
    public class Order
    {
        public int IdOrder { get; set; }
        [ForeignKey("Customer")]
        public int IdCustomer { get; set; }
        public Customer Customer { get; set; }

        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }

        [ForeignKey("Employee")]
        public int IdEmployee { get; set; }
        public Employee Employee { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public bool Pay { get; set; }
        public string Receiver { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }

        [MaxLength(13)]
        public string? OrderCode { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
