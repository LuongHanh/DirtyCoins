using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Services;
using DirtyCoins.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CustomerController(ApplicationDbContext context) => _context = context;

        // 🔹 Hàm tiện ích lấy IdUser từ claim
        private int? GetCurrentUserId()
        {
            string? idStr = User.FindFirstValue("AppUserId")
                            ?? User.FindFirstValue("IdUser");
            return int.TryParse(idStr, out int id) ? id : null;
        }

        // -------------------------------
        // 📍 Dashboard (Views/Customer/Dashboard.cshtml)
        // -------------------------------
        public IActionResult Dashboard()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var customer = _context.Customers
                .Include(c => c.User)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                .FirstOrDefault(c => c.IdUser == idUser.Value);

            if (customer == null)
                return RedirectToAction("Login", "Account");

            // 🔹 Lấy xếp hạng hiện tại của khách hàng
            var rank = _context.CustomerRankStats
                .Where(r => r.IdCustomer == customer.IdCustomer)
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Month)
                .FirstOrDefault();

            ViewBag.RankName = rank?.RankName ?? "Thành viên mới";
            ViewBag.RankDiscount = rank?.DiscountPercent ?? 0;
            ViewBag.TotalSpent = rank?.TotalSpent ?? 0;
            ViewBag.CreatedAt = customer.CreatedAt;

            return View(customer);
        }

        // -------------------------------
        // 📍 Orders (Views/Customer/Orders.cshtml)
        // -------------------------------
        public IActionResult Orders()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Where(o => o.Customer.IdUser == idUser.Value)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(orders);
        }

        // -------------------------------
        // 📍 OrderDetail (Views/Customer/OrderDetail.cshtml)
        // -------------------------------
        public async Task<IActionResult> OrderDetail(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.IdOrder == id);

            if (order == null) return NotFound();

            var productIds = order.OrderDetails.Select(d => d.IdProduct).ToList();

            var activePromotions = await _context.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => productIds.Contains(pp.IdProduct)
                    && pp.Promotion.IsActive
                    && pp.Promotion.StartDate <= DateTime.Now
                    && pp.Promotion.EndDate >= DateTime.Now)
                .ToListAsync();

            var orderItems = new List<OrderDetailItemVM>();
            decimal promoDiscount = 0;
            decimal totalAllBill = 0;

            foreach (var detail in order.OrderDetails) //lặp qua các item trong đơn hàng
            {
                var promo = activePromotions.FirstOrDefault(p => p.IdProduct == detail.IdProduct);
                var product = _context.Products.FirstOrDefault(p => p.IdProduct == detail.IdProduct);
                decimal discountPercent = promo?.Promotion?.DiscountPercent ?? 0;
                string promoName = promo?.Promotion?.PromotionName ?? "-";

                var total = product.Price * detail.Quantity; //Giá nguyên
                var originalTotal = detail.UnitPrice * detail.Quantity; //Giá đã áp dụng khuyến mãi
                var discountAmount = total - originalTotal;
                promoDiscount += discountAmount;

                totalAllBill += originalTotal;

                orderItems.Add(new OrderDetailItemVM
                {
                    Product = detail.Product,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice,
                    DiscountPercent = discountPercent,
                    PromotionName = promoName
                });
            }

            var rankStat = await _context.CustomerRankStats
                .Where(r => r.IdCustomer == order.IdCustomer)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            decimal memberDiscount = 0;
            if (rankStat != null)
                memberDiscount = totalAllBill * rankStat.DiscountPercent / 100; ;

            var vm = new OrderDetailViewModel
            {
                Order = order,
                Customer = order.Customer,
                OrderDetails = orderItems,
                PromoDiscount = promoDiscount,
                MemberDiscount = memberDiscount,
                TotalSavings = promoDiscount + memberDiscount
            };

            return View(vm);
        }

        // -------------------------------
        // 📍 Settings GET (Views/Customer/Settings.cshtml)
        // -------------------------------
        [HttpGet]
        public IActionResult Settings()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var customer = _context.Customers
                .Include(c => c.User)
                .FirstOrDefault(c => c.IdUser == idUser.Value);

            if (customer == null)
                return RedirectToAction("Dashboard");

            return View(customer);
        }

        // -------------------------------
        // 📍 Settings POST (Cập nhật thông tin)
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(Customer model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existing = _context.Customers
                .Include(c => c.User)
                .FirstOrDefault(c => c.IdCustomer == model.IdCustomer);

            if (existing == null)
                return NotFound();

            // 🔹 Cập nhật thông tin cơ bản
            existing.FullName = model.FullName;
            existing.Phone = model.Phone;
            existing.Address = model.Address;
            await _context.SaveChangesAsync();

            // 🔹 Cập nhật lại tọa độ nếu có thay đổi địa chỉ
            if (!string.IsNullOrWhiteSpace(existing.Address))
            {
                var geoService = HttpContext.RequestServices.GetRequiredService<GeocodingService>();
                var (lat, lon) = await geoService.GetCoordinatesAsync(existing.Address);

                if (lat != 0 && lon != 0)
                {
                    existing.Latitude = lat;
                    existing.Longitude = lon;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"📍 Đã cập nhật tọa độ cho {existing.FullName}: {lat}, {lon}");
                }
            }

            // 🔹 Cập nhật lại Claim trong session đăng nhập
            var claimsIdentity = (ClaimsIdentity)User.Identity!;
            var oldClaim = claimsIdentity.FindFirst("FullName");
            if (oldClaim != null)
                claimsIdentity.RemoveClaim(oldClaim);
            claimsIdentity.AddClaim(new Claim("FullName", existing.FullName));

            await HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Dashboard");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelOrder(int id)
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var order = _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.IdOrder == id && o.Customer.IdUser == idUser.Value);

            if (order == null) return NotFound();

            // chỉ cho phép hủy khi đang xử lý hoặc chờ giao hàng
            if (order.Status == "Đang xử lý" || order.Status == "Chờ giao hàng")
            {
                if (order.Pay == false)
                {
                    order.Status = "Đã hủy";
                    _context.SaveChanges();
                    TempData["Message"] = $"❌ Đơn hàng {order.OrderCode} đã được huỷ thành công.";
                }
                else
                {
                    TempData["Message"] = $"❌ Đơn hàng {order.OrderCode} không được huỷ do đã thanh toán thành công!";
                }
            }
            else
            {
                // an toàn: không thay đổi DB, báo lỗi
                TempData["Message"] = $"⚠ Không thể huỷ đơn {order.OrderCode} khi ở trạng thái “{order.Status}”.";
            }

            return RedirectToAction("Orders");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmReceived(int id)
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var order = _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.IdOrder == id && o.Customer.IdUser == idUser.Value);

            if (order == null) return NotFound();

            // chỉ cho phép xác nhận khi thực sự đang giao hàng
            if (order.Status == "Đang giao hàng")
            {
                order.Status = "Giao hàng thành công";
                order.Pay = true;
                _context.SaveChanges();
                TempData["Message"] = $"✅ Cảm ơn bạn! Đơn hàng {order.OrderCode} đã được xác nhận đã nhận.";
            }
            else
            {
                TempData["Message"] = $"⚠ Không thể xác nhận nhận hàng cho đơn {order.OrderCode} khi ở trạng thái “{order.Status}”.";
            }

            return RedirectToAction("Orders");
        }
    }
}
