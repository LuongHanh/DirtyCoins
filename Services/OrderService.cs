// Services/OrderService.cs
using DirtyCoins.Data;
using DirtyCoins.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace DirtyCoins.Services
{
    public class OrderService
    {
        private readonly ApplicationDbContext _db;
        public OrderService(ApplicationDbContext db) { _db = db; }

        // Create order in transaction: check inventory per store, reduce inventory, create order + details
        public async Task<(bool Success, string Message, int? OrderId)> CreateOrderAsync(int customerId, int storeId, List<(int productId, int qty, decimal unitPrice)> items)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                decimal total = items.Sum(i => i.qty * i.unitPrice);
                var order = new Order
                {
                    IdCustomer = customerId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = total,
                    Status = "Đang xử lý"
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Check and update inventory
                foreach (var it in items)
                {
                    var inv = await _db.Inventory.FirstOrDefaultAsync(x => x.IdStore == storeId && x.IdProduct == it.productId);
                    if (inv == null || inv.Quantity < it.qty)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Sản phẩm {it.productId} không đủ tồn kho", null);
                    }
                    inv.Quantity -= it.qty;
                    inv.LastUpdate = DateTime.UtcNow;
                    _db.OrderDetail.Add(new OrderDetail
                    {
                        IdOrder = order.IdOrder,
                        IdProduct = it.productId,
                        Quantity = it.qty,
                        UnitPrice = it.unitPrice
                    });
                }
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, "Tạo đơn hàng thành công", order.IdOrder);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, ex.Message, null);
            }
        }
    }
}
