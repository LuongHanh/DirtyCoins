// Services/ReportService.cs
using DirtyCoins.Data;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;

namespace DirtyCoins.Services
{
    public class ReportService
    {
        private readonly ApplicationDbContext _db;
        public ReportService(ApplicationDbContext db) { _db = db; }

        public async Task<(decimal Revenue, int Orders)> GetStoreSummaryAsync(int IdStore, DateTime from, DateTime to)
        {
            var orderIds = await _db.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                .Select(o => o.IdOrder)
                .ToListAsync();

            var orders = await _db.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                .ToListAsync();

            decimal revenue = orders.Sum(o => o.TotalAmount);
            int totalOrders = orders.Count;

            return (revenue, totalOrders);
        }
    }
}
