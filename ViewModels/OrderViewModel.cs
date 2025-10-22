// ViewModels/OrderViewModel.cs
using System.Collections.Generic;

namespace DirtyCoins.ViewModels
{
    public class OrderItemDto
    {
        public int IdProduct { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class OrderViewModel
    {
        public int IdCustomer { get; set; }
        public int IdStore { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
