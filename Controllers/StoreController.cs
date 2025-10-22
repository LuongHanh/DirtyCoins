using ClosedXML.Excel;
using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "StoreOwner")]
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;
        public StoreController(ApplicationDbContext context, SystemLogService logService)
        {
            _context = context;
            _logService = logService;
        }

        private int? GetCurrentUserId()
        {
            string? idStr = User.FindFirstValue("AppUserId")
                            ?? User.FindFirstValue("IdUser");
            return int.TryParse(idStr, out int id) ? id : null;
        }

        // -------------------------------
        // 📍 Dashboard (tổng quan)
        // -------------------------------
        public IActionResult Dashboard()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores
                .Include(s => s.Employees)
                .Include(s => s.Reports)
                .FirstOrDefault(s => s.IdUser == idUser.Value);

            if (store == null) return RedirectToAction("Login", "Account");

            int storeId = store.IdStore;

            ViewBag.EmployeeCount = store.Employees?.Count ?? 0;
            ViewBag.TotalReports = store.Reports?.Count ?? 0;

            // lấy tổng từ Orders
            var orders = _context.Orders.Where(o => o.IdStore == storeId);
            ViewBag.TotalOrders = orders.Count();
            ViewBag.TotalRevenue = orders.Sum(o => (decimal?)o.TotalAmount) ?? 0m;

            return View(store);
        }

        // -------------------------------
        // 📍 Employees (danh sách nhân viên)
        // -------------------------------
        public IActionResult Employees()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            var employees = _context.Employee
                .Include(e => e.User)
                .Where(e => e.IdStore == store.IdStore)
                .ToList();

            return View(employees);
        }

        // -------------------------------
        // 📍 BusinessStatus (tình trạng kinh doanh)
        // -------------------------------
        // Hiển thị BusinessStatus (GET)
        public IActionResult BusinessStatus()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            int storeId = store.IdStore;
            var now = DateTime.Now;
            var prevMonth = now.AddMonths(-1);

            // --- Load dữ liệu chi phí vận hành tháng này
            var defaultCostTypes = new List<string> { "Tiền thuê", "Điện nước", "Lương nhân viên", "Marketing" };

            var currentCosts = _context.OperationCosts
                .Where(c => c.IdStore == storeId && c.CostMonth == now.Month && c.CostYear == now.Year)
                .ToList();

            var modelCosts = defaultCostTypes.Select(type => new OperationCost
            {
                IdStore = storeId,
                CostType = type,
                CostAmount = currentCosts.FirstOrDefault(c => c.CostType == type)?.CostAmount ?? 0,
                CostMonth = now.Month,
                CostYear = now.Year
            }).ToList();

            ViewBag.Costs = modelCosts;

            // Orders
            var orders = _context.Orders.Where(o => o.IdStore == storeId).ToList();

            // Doanh thu bán hàng (offline + online) mô phỏng bằng offline(COD)
            decimal revenueThisMonth = orders.Where(o => o.OrderDate.Month == now.Month && o.OrderDate.Year == now.Year)
                                             .Sum(o => (decimal?)o.TotalAmount) ?? 0m;
            decimal revenuePrevMonth = orders.Where(o => o.OrderDate.Month == prevMonth.Month && o.OrderDate.Year == prevMonth.Year)
                                             .Sum(o => (decimal?)o.TotalAmount) ?? 0m;

            // Doanh thu online (Bank, EWallet)
            decimal onlineThisMonth = orders
                .Where(o => o.OrderDate.Month == now.Month
                         && o.OrderDate.Year == now.Year
                         && (o.PaymentMethod == "Bank" || o.PaymentMethod == "EWallet"))
                .Sum(o => (decimal?)o.TotalAmount) ?? 0m;

            decimal onlinePrevMonth = orders
                .Where(o => o.OrderDate.Month == prevMonth.Month
                         && o.OrderDate.Year == prevMonth.Year
                         && (o.PaymentMethod == "Bank" || o.PaymentMethod == "EWallet"))
                .Sum(o => (decimal?)o.TotalAmount) ?? 0m;

            // Tổng doanh thu
            decimal totalThisMonth = revenueThisMonth + onlineThisMonth;
            decimal totalPrevMonth = revenuePrevMonth + onlinePrevMonth;

            // Chi phí vận hành
            var opCosts = _context.OperationCosts.Where(c => c.IdStore == storeId);
            decimal costThisMonth = opCosts.Where(c => c.CostMonth == now.Month && c.CostYear == now.Year)
                                           .Sum(c => (decimal?)c.CostAmount) ?? 0m;
            decimal costPrevMonth = opCosts.Where(c => c.CostMonth == prevMonth.Month && c.CostYear == prevMonth.Year)
                                           .Sum(c => (decimal?)c.CostAmount) ?? 0m;

            // Lợi nhuận gộp
            decimal grossProfitThisMonth = totalThisMonth - costThisMonth;
            decimal grossProfitPrevMonth = totalPrevMonth - costPrevMonth;

            // Lợi nhuận ròng (giả sử sau thuế 22%)
            decimal netProfitThisMonth = grossProfitThisMonth * 0.78m;
            decimal netProfitPrevMonth = grossProfitPrevMonth * 0.78m;

            // Sản phẩm bán chạy nhất
            var bestProduct = _context.OrderDetail
                .Include(d => d.Product)
                .Where(d => d.Order.OrderDate.Month == now.Month && d.Order.OrderDate.Year == now.Year && d.Order.IdStore == storeId)
                .GroupBy(d => d.Product.Name)
                .Select(g => new { Product = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Quantity * x.UnitPrice) })
                .OrderByDescending(x => x.Quantity)
                .FirstOrDefault();
            ViewBag.BestProduct = bestProduct;

            // Doanh thu theo tháng
            var revenueByMonth = orders
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(x => (decimal?)x.TotalAmount) ?? 0m
                })
                .ToList();

            ViewBag.RevenueByMonthJson = JsonSerializer.Serialize(revenueByMonth);

            // Chi phí theo tháng (cho chart)
            var costByMonth = _context.OperationCosts
                .Where(c => c.IdStore == storeId)
                .GroupBy(c => new { c.CostYear, c.CostMonth })
                .Select(g => new
                {
                    Year = g.Key.CostYear,
                    Month = g.Key.CostMonth,
                    Cost = g.Sum(x => (decimal?)x.CostAmount) ?? 0m
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            ViewBag.CostByMonthJson = JsonSerializer.Serialize(costByMonth);

            // Khách trung bình (số khách / ngày)
            int totalCustomers = orders.Count;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            ViewBag.AvgCustomers = Math.Round((decimal)totalCustomers / daysInMonth, 2);

            // Growth Rate theo tháng
            ViewBag.GrowthRate = revenuePrevMonth > 0 ? Math.Round(((revenueThisMonth - revenuePrevMonth) / revenuePrevMonth) * 100, 2) : 0;

            // Gửi dữ liệu ra View
            ViewBag.RevenueThisMonth = revenueThisMonth;
            ViewBag.RevenuePrevMonth = revenuePrevMonth;

            ViewBag.OnlineThisMonth = onlineThisMonth;
            ViewBag.OnlinePrevMonth = onlinePrevMonth;

            ViewBag.TotalThisMonth = totalThisMonth;
            ViewBag.TotalPrevMonth = totalPrevMonth;

            ViewBag.CostThisMonth = costThisMonth;
            ViewBag.CostPrevMonth = costPrevMonth;

            ViewBag.GrossProfitThisMonth = grossProfitThisMonth;
            ViewBag.GrossProfitPrevMonth = grossProfitPrevMonth;

            ViewBag.NetProfitThisMonth = netProfitThisMonth;
            ViewBag.NetProfitPrevMonth = netProfitPrevMonth;

            return View(modelCosts);
        }

        // Cập nhật chi phí vận hành (POST)
        [HttpPost]
        public IActionResult UpdateOperationCosts(List<OperationCost> costs)
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            int storeId = store.IdStore;
            var now = DateTime.Now;

            foreach (var cost in costs)
            {
                var existing = _context.OperationCosts.FirstOrDefault(c =>
                    c.IdStore == storeId &&
                    c.CostType == cost.CostType &&
                    c.CostMonth == now.Month &&
                    c.CostYear == now.Year);

                if (existing != null)
                {
                    existing.CostAmount = cost.CostAmount;
                    existing.Note = cost.Note;
                }
                else
                {
                    cost.IdStore = storeId;
                    cost.CostMonth = now.Month;
                    cost.CostYear = now.Year;
                    _context.OperationCosts.Add(cost);
                }
            }

            _context.SaveChanges();
            TempData["Success"] = "Cập nhật chi phí thành công!";
            _logService.LogAsync(idUser.Value, $"Cửa hàng {storeId} cập nhật chi phí vận hành thành công");
            return RedirectToAction("BusinessStatus");
        }

        // -------------------------------
        // 📍 SalesStatus (tình trạng bán hàng)
        // -------------------------------
        public IActionResult SalesStatus()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            int storeId = store.IdStore;
            var now = DateTime.Now;

            // Lấy dữ liệu từ MonthlyInventory
            var monthlyData = _context.MonthlyInventorys
                .Include(m => m.Product)
                    .ThenInclude(p => p.Category)
                .Where(m => m.IdStore == storeId && m.Month == now.Month && m.Year == now.Year)
                .ToList();

            // Khuyến mãi
            var promo = _context.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => pp.Promotion.IsActive
                             && pp.Promotion.StartDate <= now
                             && pp.Promotion.EndDate >= now)
                .ToDictionary(pp => pp.IdProduct, pp => pp.Promotion.DiscountPercent ?? 0);

            // Tạo danh sách hiển thị
            var list = monthlyData.Select(m =>
            {
                string status = m.SoldQty > m.BeginQty * 0.6 ? "Bán chạy"
                               : m.EndQty < 10 ? "Sắp hết"
                               : m.EndQty > 50 ? "Tồn nhiều"
                               : "Bình thường";

                return new
                {
                    Code = "SP" + m.IdProduct.ToString("D3"),
                    Product = m.Product?.Name ?? "-",
                    Category = m.Product?.Category?.Name ?? "-",
                    Price = m.Product?.Price ?? 0,
                    BeginQty = m.BeginQty,
                    Imported = m.ImportedQty,
                    Sold = m.SoldQty,
                    EndQty = m.EndQty,
                    Discount = promo.ContainsKey(m.IdProduct) ? promo[m.IdProduct] : 0,
                    Status = status
                };
            }).ToList();

            // Biểu đồ bán theo nhóm loại
            var chartByCategory = list.GroupBy(x => x.Category)
                .Select(g => new { Category = g.Key, Sold = g.Sum(x => x.Sold) })
                .ToList();

            ViewBag.SalesTable = list;
            ViewBag.ChartByCategoryJson = JsonSerializer.Serialize(chartByCategory);

            // Top 5 sản phẩm bán chạy
            var topBest = list.OrderByDescending(x => x.Sold).Take(5).ToList();

            // Top 5 sản phẩm bán chậm (ít bán hoặc tồn nhiều)
            var topSlow = list.OrderBy(x => x.Sold).ThenByDescending(x => x.EndQty).Take(5).ToList();

            ViewBag.ChartByCategoryJson = JsonSerializer.Serialize(chartByCategory);
            ViewBag.ChartBestJson = JsonSerializer.Serialize(topBest);
            ViewBag.ChartSlowJson = JsonSerializer.Serialize(topSlow);

            return View();
        }

        // -------------------------------
        // 📍 Reports (báo cáo tổng hợp)
        // -------------------------------
        public IActionResult Reports()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            int storeId = store.IdStore;
            var now = DateTime.Now;
            var prevMonth = now.AddMonths(-1);

            // ===================== KINH DOANH =====================
            var orders = _context.Orders.Where(o => o.IdStore == storeId).ToList();

            decimal revenueThis = orders.Where(o => o.OrderDate.Month == now.Month && o.OrderDate.Year == now.Year)
                                        .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenuePrev = orders.Where(o => o.OrderDate.Month == prevMonth.Month && o.OrderDate.Year == prevMonth.Year)
                                        .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // Online vs COD
            decimal revenueOnlineThis = orders.Where(o => o.OrderDate.Month == now.Month && o.PaymentMethod != "COD")
                                              .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenueOnlinePrev = orders.Where(o => o.OrderDate.Month == prevMonth.Month && o.PaymentMethod != "COD")
                                              .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenueCODThis = revenueThis - revenueOnlineThis;
            decimal revenueCODPrev = revenuePrev - revenueOnlinePrev;

            // Chi phí vận hành
            var costs = _context.OperationCosts.Where(c => c.IdStore == storeId).ToList();
            decimal costThis = costs.Where(c => c.CostMonth == now.Month && c.CostYear == now.Year).Sum(c => c.CostAmount);
            decimal costPrev = costs.Where(c => c.CostMonth == prevMonth.Month && c.CostYear == prevMonth.Year).Sum(c => c.CostAmount);

            // Lợi nhuận
            decimal grossThis = revenueThis - costThis;
            decimal grossPrev = revenuePrev - costPrev;
            decimal netThis = grossThis * 0.9m; // giả sử thuế 10%
            decimal netPrev = grossPrev * 0.9m;

            // Best seller
            var bestProduct = _context.OrderDetail
                .Include(od => od.Product)
                .Where(od => od.Order.IdStore == storeId && od.Order.OrderDate.Month == now.Month && od.Order.OrderDate.Year == now.Year)
                .GroupBy(od => od.Product.Name)
                .Select(g => new { Product = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(g => g.Quantity)
                .FirstOrDefault();
            // Khách trung bình (số khách / ngày)
            int totalCustomers = orders.Count;
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            ViewBag.AvgCustomers = Math.Round((decimal)totalCustomers / daysInMonth, 2);

            ViewBag.BusinessReport = new
            {
                RevenueCODThis = revenueCODThis,
                RevenueCODPrev = revenueCODPrev,
                RevenueOnlineThis = revenueOnlineThis,
                RevenueOnlinePrev = revenueOnlinePrev,
                RevenueThis = revenueThis,
                RevenuePrev = revenuePrev,
                CostThis = costThis,
                CostPrev = costPrev,
                GrossThis = grossThis,
                GrossPrev = grossPrev,
                NetThis = netThis,
                NetPrev = netPrev,
                GrowthRate = revenuePrev > 0 ? Math.Round((revenueThis - revenuePrev) / revenuePrev * 100, 2) : 0,
                BestProduct = bestProduct
            };

            // ===================== BÁN HÀNG =====================
            var monthlyData = _context.MonthlyInventorys
                .Include(m => m.Product).ThenInclude(p => p.Category)
                .Where(m => m.IdStore == storeId && m.Month == now.Month && m.Year == now.Year)
                .ToList();

            ViewBag.SalesReport = monthlyData.Select(m => new
            {
                Code = "SP" + m.IdProduct.ToString("D3"),
                Product = m.Product?.Name ?? "-",
                Category = m.Product?.Category?.Name ?? "-",
                Price = m.Product?.Price ?? 0,
                BeginQty = m.BeginQty,
                Imported = m.ImportedQty,
                Sold = m.SoldQty,
                EndQty = m.EndQty,
                Discount = 0,
                Status = m.SoldQty > m.BeginQty * 0.6 ? "Bán chạy"
                          : m.EndQty < 5 ? "Sắp hết"
                          : m.EndQty > 50 ? "Tồn nhiều"
                          : "Bình thường"
            }).ToList();

            ViewBag.Store = store;
            return View();
        }

        // ===================== XUẤT EXCEL =====================
        public IActionResult ExportReportExcel()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue) return RedirectToAction("Login", "Account");

            var store = _context.Stores.FirstOrDefault(s => s.IdUser == idUser.Value);
            if (store == null) return RedirectToAction("Dashboard");

            int storeId = store.IdStore;
            var now = DateTime.Now;
            var prevMonth = now.AddMonths(-1);

            // ===================== KINH DOANH =====================
            var orders = _context.Orders.Where(o => o.IdStore == storeId).ToList();

            decimal revenueThis = orders.Where(o => o.OrderDate.Month == now.Month && o.OrderDate.Year == now.Year)
                                        .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenuePrev = orders.Where(o => o.OrderDate.Month == prevMonth.Month && o.OrderDate.Year == prevMonth.Year)
                                        .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            decimal revenueOnlineThis = orders.Where(o => o.OrderDate.Month == now.Month && o.PaymentMethod != "COD")
                                              .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenueOnlinePrev = orders.Where(o => o.OrderDate.Month == prevMonth.Month && o.PaymentMethod != "COD")
                                              .Sum(o => (decimal?)o.TotalAmount) ?? 0;
            decimal revenueCODThis = revenueThis - revenueOnlineThis;
            decimal revenueCODPrev = revenuePrev - revenueOnlinePrev;

            var costs = _context.OperationCosts.Where(c => c.IdStore == storeId).ToList();
            decimal costThis = costs.Where(c => c.CostMonth == now.Month && c.CostYear == now.Year).Sum(c => c.CostAmount);
            decimal costPrev = costs.Where(c => c.CostMonth == prevMonth.Month && c.CostYear == prevMonth.Year).Sum(c => c.CostAmount);

            decimal grossThis = revenueThis - costThis;
            decimal grossPrev = revenuePrev - costPrev;
            decimal netThis = grossThis * 0.9m;
            decimal netPrev = grossPrev * 0.9m;

            var bestProduct = _context.OrderDetail
                .Include(od => od.Product)
                .Where(od => od.Order.IdStore == storeId && od.Order.OrderDate.Month == now.Month && od.Order.OrderDate.Year == now.Year)
                .GroupBy(od => od.Product.Name)
                .Select(g => new { Product = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(g => g.Quantity)
                .FirstOrDefault();

            var monthlyData = _context.MonthlyInventorys
                .Include(m => m.Product).ThenInclude(p => p.Category)
                .Where(m => m.IdStore == storeId && m.Month == now.Month && m.Year == now.Year)
                .ToList();

            using (var workbook = new XLWorkbook())
            {
                // ===== SHEET A: KINH DOANH =====
                var wsA = workbook.Worksheets.Add("Kinh doanh");
                wsA.Cell("A1").Value = $"BÁO CÁO KINH DOANH THÁNG {now.Month}/{now.Year}";
                wsA.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                wsA.Range("A1:E1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsA.Cell("A3").Value = "Chỉ tiêu";
                wsA.Cell("B3").Value = "Tháng này";
                wsA.Cell("C3").Value = "Tháng trước";
                wsA.Cell("D3").Value = "Chênh lệch";
                wsA.Cell("E3").Value = "Thay đổi (%)";
                wsA.Range("A3:E3").Style.Font.Bold = true;
                wsA.Range("A3:E3").Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 4;
                void AddRow(string label, decimal thisVal, decimal prevVal)
                {
                    wsA.Cell(row, 1).Value = label;
                    wsA.Cell(row, 2).Value = thisVal;
                    wsA.Cell(row, 3).Value = prevVal;
                    wsA.Cell(row, 4).Value = thisVal - prevVal;
                    wsA.Cell(row, 5).Value = prevVal > 0 ? Math.Round((thisVal - prevVal) / prevVal * 100, 2) : 0;
                    row++;
                }

                AddRow("Doanh thu COD", revenueCODThis, revenueCODPrev);
                AddRow("Doanh thu Online", revenueOnlineThis, revenueOnlinePrev);
                AddRow("Tổng doanh thu", revenueThis, revenuePrev);
                AddRow("Chi phí vận hành", costThis, costPrev);
                AddRow("Lợi nhuận gộp", grossThis, grossPrev);
                AddRow("Lợi nhuận ròng (sau thuế 10%)", netThis, netPrev);

                wsA.Cell(row + 1, 1).Value = "Sản phẩm bán chạy nhất";
                wsA.Cell(row + 1, 2).Value = bestProduct?.Product ?? "Không có dữ liệu";
                wsA.Cell(row + 1, 3).Value = bestProduct?.Quantity ?? 0;

                wsA.Columns().AdjustToContents();

                // ===== SHEET B: BÁN HÀNG =====
                var wsB = workbook.Worksheets.Add("Bán hàng");
                wsB.Cell("A1").Value = $"BÁO CÁO TÌNH TRẠNG BÁN HÀNG THÁNG {now.Month}/{now.Year}";
                wsB.Range("A1:J1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                wsB.Range("A1:J1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                string[] headers = { "Mã SP", "Tên sản phẩm", "Danh mục", "Giá", "Tồn đầu", "Nhập", "Bán", "Tồn cuối", "Giảm giá", "Trạng thái" };
                for (int i = 0; i < headers.Length; i++)
                {
                    wsB.Cell(3, i + 1).Value = headers[i];
                    wsB.Cell(3, i + 1).Style.Font.Bold = true;
                    wsB.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int r = 4;
                foreach (var m in monthlyData)
                {
                    wsB.Cell(r, 1).Value = "SP" + m.IdProduct.ToString("D3");
                    wsB.Cell(r, 2).Value = m.Product?.Name ?? "-";
                    wsB.Cell(r, 3).Value = m.Product?.Category?.Name ?? "-";
                    wsB.Cell(r, 4).Value = m.Product?.Price ?? 0;
                    wsB.Cell(r, 5).Value = m.BeginQty;
                    wsB.Cell(r, 6).Value = m.ImportedQty;
                    wsB.Cell(r, 7).Value = m.SoldQty;
                    wsB.Cell(r, 8).Value = m.EndQty;
                    wsB.Cell(r, 9).Value = 0;
                    wsB.Cell(r, 10).Value = m.SoldQty > m.BeginQty * 0.6 ? "Bán chạy"
                        : m.EndQty < 5 ? "Sắp hết"
                        : m.EndQty > 50 ? "Tồn nhiều"
                        : "Bình thường";
                    r++;
                }

                wsB.Columns().AdjustToContents();

                // Xuất file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "BaoCaoTinhTrangKinhDoanh.xlsx");
                }
            }
        }
    }
}
