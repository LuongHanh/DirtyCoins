using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Helpers; // 👈 thêm namespace helper
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DirtyCoins.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // -------------------------------
        // 📍 Trang chủ
        // -------------------------------
        public async Task<IActionResult> Index()
        {
            // ✅ 1. Lấy chi nhánh hiện tại từ Session
            var selectedStoreId = HttpContext.Session.GetInt32("SelectedStore");

            // ✅ 2. Nếu chưa có chi nhánh thì tự xác định chi nhánh gần nhất (dựa vào tọa độ khách hàng)
            if (!selectedStoreId.HasValue && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirstValue("AppUserId");
                if (int.TryParse(userIdClaim, out int idUser))
                {
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.IdUser == idUser);

                    if (customer != null && customer.Latitude.HasValue && customer.Longitude.HasValue)
                    {
                        var stores = await _context.Stores.ToListAsync();
                        double minDistance = double.MaxValue;
                        Store nearestStore = null;

                        foreach (var s in stores)
                        {
                            if (!s.Latitude.HasValue || !s.Longitude.HasValue) continue;

                            double distance = DirtyCoins.Helpers.LocationHelper.CalculateDistance(
                                customer.Latitude.Value,
                                customer.Longitude.Value,
                                s.Latitude.Value,
                                s.Longitude.Value
                            );

                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestStore = s;
                            }
                        }

                        if (nearestStore != null)
                        {
                            selectedStoreId = nearestStore.IdStore;
                            HttpContext.Session.SetInt32("SelectedStore", nearestStore.IdStore);
                            ViewBag.NearestStore = nearestStore;
                        }

                        ViewBag.Stores = stores;
                    }
                }
            }

            // ✅ 3. Nếu vẫn chưa có, lấy tạm chi nhánh đầu tiên
            if (!selectedStoreId.HasValue)
                selectedStoreId = await _context.Stores.Select(s => s.IdStore).FirstOrDefaultAsync();

            int idStore = selectedStoreId.Value;

            // ✅ 4. Lấy danh sách chi nhánh (hiển thị dropdown)
            ViewBag.Stores = await _context.Stores.ToListAsync();
            ViewBag.SelectedStoreId = idStore;
            ViewBag.NearestStore = await _context.Stores.FirstOrDefaultAsync(s => s.IdStore == idStore);

            // ✅ 5. Lọc sản phẩm theo IdStore
            var hotProducts = await _context.Products
                .Where(p => p.IdStore == idStore)
                .OrderByDescending(p => p.IdProduct)
                .Take(8)
                .Include(p => p.Category)
                .ToListAsync();

            // ✅ 6. Lọc sản phẩm khuyến mãi theo IdStore
            var promoProducts = await _context.PromotionProducts
                .Include(pp => pp.Product)
                .Include(pp => pp.Promotion)
                .Where(pp =>
                    pp.Product.IdStore == idStore &&
                    pp.Promotion.IsActive &&
                    pp.Promotion.StartDate <= DateTime.UtcNow &&
                    pp.Promotion.EndDate >= DateTime.UtcNow)
                .Select(pp => new
                {
                    IdProduct = pp.Product.IdProduct,
                    Name = pp.Product.Name,
                    Image = pp.Product.Image,
                    Price = pp.Product.Price,
                    Discount = pp.Promotion.DiscountPercent ?? 0,
                    DiscountedPrice = Math.Round(pp.Product.Price * (1 - (pp.Promotion.DiscountPercent ?? 0) / 100), 0)
                })
                .Distinct()
                .Take(8)
                .ToListAsync();

            // ✅ 7. Truyền dữ liệu ra View
            ViewBag.HotProducts = hotProducts;
            ViewBag.PromoProducts = promoProducts;

            return View();
        }

        // -------------------------------
        // 📍 Đổi chi nhánh thủ công
        // -------------------------------
        public IActionResult ChangeStore(int id)
        {
            var store = _context.Stores.FirstOrDefault(s => s.IdStore == id);
            if (store != null)
                HttpContext.Session.SetInt32("SelectedStore", store.IdStore);

            return RedirectToAction("Index","Product");
        }

        // -------------------------------
        // 📍 Các chương trình khuyến mãi
        // -------------------------------
        public async Task<IActionResult> Events()
        {
            var promotions = await _context.Promotions
                .Include(p => p.PromotionProducts)
                    .ThenInclude(pp => pp.Product)
                .Where(p => p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return View(promotions);
        }

        // -------------------------------
        // 📍 Chính sách bảo mật
        // -------------------------------
        public IActionResult Privacy() => View();

        // -------------------------------
        // 📍 Liên hệ (GET)
        // -------------------------------
        [HttpGet]
        public async Task<IActionResult> Contact()
        {
            Contact model = new Contact();

            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirstValue("AppUserId");

                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int idUser = int.Parse(userIdClaim);
                    var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.IdUser == idUser);
                    var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.IdUser == idUser);

                    if (customer != null)
                    {
                        model.Name = customer.FullName;
                        model.Email = user.Email;
                    }
                }
            }

            // 🔹 Lấy danh sách cửa hàng
            ViewBag.Stores = await _context.Stores
                .AsNoTracking()
                .Select(s => new { s.IdStore, s.StoreName })
                .ToListAsync();

            return View(model);
        }


        // -------------------------------
        // 📍 Liên hệ (POST)
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(Contact model)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để gửi liên hệ!",
                    redirectUrl = Url.Action("Login", "Account")
                });
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng điền đầy đủ thông tin hợp lệ."
                });
            }

            var idUserClaim = User.FindFirstValue("AppUserId");
            if (!string.IsNullOrEmpty(idUserClaim))
                model.IdUser = int.Parse(idUserClaim);

            model.CreatedAt = DateTime.Now;
            model.IsHandled = false;

            // ✅ Đảm bảo IdStore được gán từ form
            if (model.IdStore == 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng chọn cửa hàng muốn gửi liên hệ."
                });
            }

            _context.Contacts.Add(model);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể."
            });
        }
    }
}
