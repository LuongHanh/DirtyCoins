using DirtyCoins.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "Director")]
    public class DirectorController : Controller
    {
        private readonly ApplicationDbContext _context;
        public DirectorController(ApplicationDbContext context) => _context = context;

        public IActionResult Dashboard()
        {
            // Thống kê tổng thể toàn hệ thống
            var totalStores = _context.Stores.Count();
            var totalEmployees = _context.Employee.Count();
            var totalOrders = _context.Reports.Sum(r => (int?)r.TotalOrders) ?? 0;
            var totalRevenue = _context.Reports.Sum(r => (decimal?)r.TotalRevenue) ?? 0;

            // Lấy dữ liệu doanh thu 6 tháng gần nhất để vẽ biểu đồ
            var revenueChart = _context.Reports
                .AsEnumerable()
                .GroupBy(r => new {
                    Month = r.Date.Month,
                    Year = r.Date.Year
                })
                .Select(g => new
                {
                    Period = $"{g.Key.Month}/{g.Key.Year}",
                    Revenue = g.Sum(x => (decimal?)x.TotalRevenue) ?? 0
                })
                .OrderBy(x => x.Period)
                .TakeLast(6)
                .ToList();

            // Lấy top chi nhánh theo doanh thu
            var topStores = _context.Stores
                .Select(s => new
                {
                    s.StoreName,
                    Revenue = _context.Reports
                        .Where(r => r.IdStore == s.IdStore)
                        .Sum(r => (decimal?)r.TotalRevenue) ?? 0
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToList();

            // Doanh thu từng cửa hàng trong tháng hiện tại
            var now = DateTime.Now;
            var monthlyStores = _context.Stores
                .Select(s => new
                {
                    s.StoreName,
                    Revenue = _context.Reports
                        .Where(r => r.IdStore == s.IdStore &&
                                    r.Date.Month == now.Month &&
                                    r.Date.Year == now.Year)
                        .Sum(r => (decimal?)r.TotalRevenue) ?? 0
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            ViewBag.TotalStores = totalStores;
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.RevenueChart = revenueChart;
            ViewBag.TopStores = topStores;
            ViewBag.MonthlyStores = monthlyStores;
            return View();
        }

        // -------------------------------
        // 📍 Báo cáo theo chi nhánh
        // -------------------------------
        public IActionResult StoresReport()
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var stores = _context.Stores
                .Include(s => s.Employees)
                .Include(s => s.Reports)
                .Include(s => s.MonthlyInventories)
                    .ThenInclude(mi => mi.Product)
                .Select(s => new
                {
                    Store = s,
                    Employees = s.Employees.ToList(),
                    Reports = s.Reports
                        .OrderByDescending(r => r.Date)
                        .Take(6)
                        .ToList(),
                    Inventories = s.MonthlyInventories
                        .Where(mi => mi.Month == currentMonth && mi.Year == currentYear)
                        .ToList()
                })
                .ToList();

            return View(stores);
        }

        // -------------------------------
        // 📍 Tổng hợp hệ thống
        // -------------------------------
        public IActionResult SystemSummary()
        {
            var summary = new
            {
                TotalStores = _context.Stores.Count(),
                TotalEmployees = _context.Employee.Count(),
                TotalCustomers = _context.Customers.Count(),
                TotalOrders = _context.Orders.Count(),
                TotalProducts = _context.Products.Count(),
                TotalRevenue = _context.Reports.Sum(r => (decimal?)r.TotalRevenue) ?? 0
            };

            return View(summary); // ✅ Views/Director/SystemSummary.cshtml
        }

        // -------------------------------
        // 📍 Phân tích dữ liệu (Analytics)
        // -------------------------------
        // DirectorController.cs
        public IActionResult Analytics()
        {
            var now = DateTime.Now;
            var sixMonthsAgo = now.AddMonths(-5);

            // 1️⃣ Doanh số theo tháng
            var monthlyRevenue = _context.Reports
                .Where(r => r.Date >= sixMonthsAgo)
                .GroupBy(r => new { r.Date.Year, r.Date.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalRevenue = g.Sum(r => (decimal?)r.TotalRevenue) ?? 0m,
                    TotalOrders = g.Sum(r => (int?)r.TotalOrders) ?? 0
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToList();

            // 2️⃣ Tồn kho hiện tại
            var inventory = _context.MonthlyInventorys
                .Where(mi => mi.Month == now.Month && mi.Year == now.Year)
                .GroupBy(mi => 1)
                .Select(g => new {
                    TotalBegin = g.Sum(x => x.BeginQty),
                    TotalImported = g.Sum(x => x.ImportedQty),
                    TotalSold = g.Sum(x => x.SoldQty),
                    TotalEnd = g.Sum(x => x.EndQty)
                }).FirstOrDefault();

            // 3️⃣ Dòng tiền = Doanh thu - Chi phí
            var cashFlow = _context.OperationCosts
                .Where(c => c.CostMonth == now.Month && c.CostYear == now.Year)
                .GroupBy(c => 1)
                .Select(g => g.Sum(x => x.CostAmount))
                .FirstOrDefault();

            var revenueThisMonth = monthlyRevenue
                .Where(m => m.Year == now.Year && m.Month == now.Month)
                .Sum(m => m.TotalRevenue);

            var netCash = revenueThisMonth - cashFlow;

            // 4️⃣ Công nợ
            var receivables = _context.Orders
                .Where(o => o.Pay == false || o.Status == "Chưa thanh toán")
                .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // 5️⃣ Hiệu quả khuyến mãi
            var promoRevenue = (from o in _context.Orders
                                join od in _context.OrderDetail on o.IdOrder equals od.IdOrder
                                join pp in _context.PromotionProducts on od.IdProduct equals pp.IdProduct
                                join p in _context.Promotions on pp.IdPromotion equals p.IdPromotion
                                where p.IsActive && o.OrderDate >= sixMonthsAgo
                                select (decimal?)(od.Quantity * od.UnitPrice)).Sum() ?? 0m;

            var totalRevenue = monthlyRevenue.Sum(m => m.TotalRevenue);
            var promoEffectiveness = totalRevenue > 0 ? (promoRevenue / totalRevenue) * 100 : 0;

            // 6️⃣ Đầu tư
            var investments = _context.OperationCosts
                .Where(c => c.CostType.Contains("Đầu tư") || c.CostType.Contains("Mở rộng"))
                .OrderByDescending(c => c.CostYear).ThenByDescending(c => c.CostMonth)
                .Take(5)
                .ToList();

            // 7️⃣ Phân tích khách hàng
            var totalCustomers = _context.Customers.Count();

            var newCustomers = _context.Customers
                .Count(c => c.CreatedAt.Month == now.Month && c.CreatedAt.Year == now.Year);

            var customerOrders = (from o in _context.Orders
                                  join c in _context.Customers on o.IdCustomer equals c.IdCustomer
                                  where o.OrderDate >= sixMonthsAgo
                                  select new { c.IdCustomer, o.IdOrder })
                                  .ToList();

            var customerOrderCounts = customerOrders
                .GroupBy(x => x.IdCustomer)
                .Select(g => new { CustomerId = g.Key, Orders = g.Count() })
                .ToList();

            var repeatCustomers = customerOrderCounts.Count(c => c.Orders > 1);
            var retentionRate = customerOrderCounts.Any()
                ? (double)repeatCustomers / customerOrderCounts.Count * 100
                : 0;

            var avgFeedback = _context.Feedbacks.Any() ? _context.Feedbacks.Average(f => f.Rating) : 0;
            var totalFeedbacks = _context.Feedbacks.Count();

            var customers = new
            {
                TotalCustomers = totalCustomers,
                NewCustomers = newCustomers,
                RetentionRate = retentionRate,
                AvgFeedback = avgFeedback,
                TotalFeedbacks = totalFeedbacks
            };

            // 8️⃣ Phân bổ khách hàng theo vị trí
            var customerLocations = _context.Customers
                .Where(c => c.Latitude != null && c.Longitude != null)
                .Select(c => new {
                    c.IdCustomer,
                    c.FullName,
                    c.Latitude,
                    c.Longitude
                })
                .ToList();

            // 9️⃣ Định vị cửa hàng
            var storeLocations = _context.Stores
                .Where(s => s.Latitude != null && s.Longitude != null)
                .Select(s => new {
                    s.IdStore,
                    s.StoreName,
                    s.Address,
                    s.Latitude,
                    s.Longitude
                }).ToList();

            // Kết hợp model
            var model = new
            {
                MonthlyRevenue = monthlyRevenue,
                Inventory = inventory,
                CashFlow = new { Revenue = revenueThisMonth, Cost = cashFlow, Net = netCash },
                Receivables = receivables,
                Promotion = new { PromoRevenue = promoRevenue, Effectiveness = promoEffectiveness },
                Investments = investments,
                Customers = customers,
                CustomerLocations = customerLocations,
                StoreLocations = storeLocations
            };

            return View(model);
        }
    }
}
