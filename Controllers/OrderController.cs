using DirtyCoins.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DirtyCoins.Services;
using DirtyCoins.ViewModels;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "Customer")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly OrderService _orderService;

        public OrderController(ApplicationDbContext db, OrderService orderService)
        {
            _db = db;
            _orderService = orderService;
        }

        // ✅ Hiển thị danh sách đơn hàng của khách hàng hiện tại
        public async Task<IActionResult> Index()
        {
            // Giả sử user đã đăng nhập, lấy IdCustomer qua session / claim
            var email = User.Identity?.Name;
            var customer = await _db.Customers.Include(c => c.User)
                                              .FirstOrDefaultAsync(c => c.User.Email == email);
            if (customer == null) return RedirectToAction("Login", "Account");

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .Where(o => o.IdCustomer == customer.IdCustomer)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders); // ✅ Views/Order/Index.cshtml
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(OrderViewModel vm)
        {
            // 🔹 Lấy danh sách Id sản phẩm cần kiểm tra
            var productIds = vm.Items.Select(i => i.IdProduct).ToList();

            // 🔹 Lấy khuyến mãi đang còn hiệu lực cho các sản phẩm đó
            var promoProducts = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => productIds.Contains(pp.IdProduct) &&
                             pp.Promotion.IsActive &&
                             pp.Promotion.StartDate <= DateTime.UtcNow &&
                             pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            // 🔹 Tính lại giá sản phẩm (nếu có khuyến mãi)
            var recalculatedItems = new List<(int IdProduct, int Quantity, decimal UnitPrice)>();

            foreach (var item in vm.Items)
            {
                var promo = promoProducts.FirstOrDefault(p => p.IdProduct == item.IdProduct);
                decimal finalPrice = item.UnitPrice;

                if (promo != null && promo.Promotion.DiscountPercent.HasValue)
                {
                    var discount = promo.Promotion.DiscountPercent.Value;
                    finalPrice = Math.Round(item.UnitPrice * (1 - discount / 100m), 0);
                }

                recalculatedItems.Add((item.IdProduct, item.Quantity, finalPrice));
            }

            // 🔹 Tạo đơn hàng qua service
            var result = await _orderService.CreateOrderAsync(vm.IdCustomer, vm.IdStore, recalculatedItems);

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Checkout");
            }

            TempData["Success"] = $"Đơn hàng #{result.OrderId} đã được tạo thành công.";
            return RedirectToAction("Index");
        }
    }
}
