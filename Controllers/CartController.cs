using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace DirtyCoins.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _db;
        private const string CART_KEY = "cart";
        private const string ORDER_CODE_KEY = "order_code"; // key lưu mã đơn hàng trong session
        public CartController(ApplicationDbContext db) => _db = db;

        // -------------------------------
        // Hàm tạo MÃ ĐƠN HÀNG ORDER_CODE_KEY
        // -------------------------------
        private static string ToBase36(long value)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (value == 0) return "0";

            string result = "";
            while (value > 0)
            {
                result = chars[(int)(value % 36)] + result;
                value /= 36;
            }
            return result;
        }

        private string GenerateOrderCode(int idCustomer)
        {
            // 🔹 Ghép chuỗi dựa vào IdCustomer + thời gian
            string raw = $"{idCustomer}-{DateTime.UtcNow.Ticks}";

            // 🔹 Tạo giá trị băm (hash) từ chuỗi => luôn vừa với kiểu long
            long hash = Math.Abs(raw.GetHashCode());

            // 🔹 Chuyển sang Base36 để ngắn gọn
            string base36 = ToBase36(hash);

            // 🔹 Đảm bảo đúng 13 ký tự (DC + 11)
            if (base36.Length < 11)
                base36 = base36.PadRight(11, 'X');
            if (base36.Length > 11)
                base36 = base36.Substring(base36.Length - 11);

            // 🔹 Thêm prefix "DC" → tổng cộng 13 ký tự
            return $"DC{base36.ToUpper()}";
        }

        // -------------------------------
        // 📍 CartItem lưu trong cookie
        // -------------------------------
        public class CartItem
        {
            public int IdProduct { get; set; }
            public int Quantity { get; set; }
        }

        // -------------------------------
        // 📍 Lấy giỏ hàng từ cookie
        // -------------------------------
        private List<CartItem> GetCart()
        {
            var json = Request.Cookies[CART_KEY];
            if (string.IsNullOrEmpty(json)) return new();
            try
            {
                return JsonConvert.DeserializeObject<List<CartItem>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }

        // -------------------------------
        // 📍 Lưu giỏ hàng vào cookie
        // -------------------------------
        private void SaveCart(List<CartItem> cart)
        {
            var json = JsonConvert.SerializeObject(cart);
            Response.Cookies.Append(CART_KEY, json, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(7) // sống 7 ngày
            });
        }

        // -------------------------------
        // 📍 GET: /Cart
        // -------------------------------
        public async Task<IActionResult> Index()
        {
            var cart = GetCart();
            if (cart == null || !cart.Any())
            {
                ViewBag.Total = 0;
                return View(new List<CartItemViewModel>());
            }

            var ids = cart.Select(c => c.IdProduct).ToList();
            var products = await _db.Products
                .Where(p => ids.Contains(p.IdProduct))
                .ToListAsync();

            // 🔹 Lấy danh sách khuyến mãi đang còn hiệu lực
            var activePromos = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp =>
                    ids.Contains(pp.IdProduct) &&
                    pp.Promotion.IsActive &&
                    pp.Promotion.StartDate <= DateTime.UtcNow &&
                    pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            // 🔹 Map sang ViewModel
            var model = cart.Select(c =>
            {
                var p = products.FirstOrDefault(x => x.IdProduct == c.IdProduct);
                if (p == null) return null;

                // Tìm khuyến mãi áp dụng cho sản phẩm này (nếu có)
                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                var discount = promo?.Promotion?.DiscountPercent ?? 0m;

                return new CartItemViewModel
                {
                    Product = p,
                    Quantity = c.Quantity,
                    DiscountPercent = discount
                };
            })
            .Where(x => x != null)
            .ToList();

            // 🔹 Tổng cộng
            ViewBag.Total = model.Sum(m => m.SubTotal);
            return View(model);
        }

        // -------------------------------
        // 📍 POST: Add to Cart
        // -------------------------------
        [HttpPost]
        public IActionResult AddToCart(int IdProduct, int quantity = 1)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.IdProduct == IdProduct);
            if (item != null)
                item.Quantity += quantity;
            else
                cart.Add(new CartItem { IdProduct = IdProduct, Quantity = quantity });

            SaveCart(cart);
            // Trả về JSON cho AJAX
            return Json(new
            {
                message = $"Đã thêm {quantity} sản phẩm vào giỏ hàng!",
                cartCount = cart.Sum(c => c.Quantity)
            });
        }

        // -------------------------------
        // 📍 POST: Remove from Cart
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int IdProduct)
        {
            var cart = GetCart(); // List<CartItemViewModel>
            if (cart == null) cart = new List<CartItem>();

            // Xoá sản phẩm
            cart.RemoveAll(c => c.IdProduct == IdProduct);
            SaveCart(cart);

            // Lấy thông tin sản phẩm từ DB để tính subtotal và total
            var ids = cart.Select(c => c.IdProduct).ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.IdProduct)).ToListAsync();

            var activePromos = await _db.PromotionProducts.Include(pp => pp.Promotion)
                .Where(pp => ids.Contains(pp.IdProduct) &&
                             pp.Promotion.IsActive &&
                             pp.Promotion.StartDate <= DateTime.UtcNow &&
                             pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            // Map sang ViewModel để tính subtotal
            var model = cart.Select(c =>
            {
                var p = products.FirstOrDefault(x => x.IdProduct == c.IdProduct);
                if (p == null) return null;
                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                var discount = promo?.Promotion?.DiscountPercent ?? 0;

                return new CartItemViewModel
                {
                    Product = p,
                    Quantity = c.Quantity,
                    DiscountPercent = discount
                };
            })
            .Where(x => x != null)
            .ToList();

            var total = model.Sum(x => x.SubTotal);
            var cartCount = model.Sum(x => x.Quantity);

            return Json(new { total, cartCount });
        }

        // -------------------------------
        // 📍 GET: Checkout
        // -------------------------------
        [HttpGet]
        public async Task<IActionResult> Checkout(string productIds)
        {
            var cart = GetCart();
            if (!cart.Any()) return RedirectToAction("Index");

            // Lấy thông tin user
            var userIdClaim = User.FindFirstValue("AppUserId");
            if (!int.TryParse(userIdClaim, out int idUser))
                return RedirectToAction("Login", "Account");

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.IdUser == idUser);
            if (customer == null) return RedirectToAction("Index");

            // 🔹 Tạo mã đơn hàng duy nhất
            string orderCode = GenerateOrderCode(customer.IdCustomer);
            HttpContext.Session.SetString(ORDER_CODE_KEY, orderCode);

            // 🔹 Lọc sản phẩm được chọn
            List<int> selectedIds = null;
            if (!string.IsNullOrEmpty(productIds))
            {
                selectedIds = productIds.Split(',').Select(int.Parse).ToList();
                HttpContext.Session.SetString("SelectedCartItems", string.Join(",", selectedIds));
            }

            var productsInCart = cart.Where(c => selectedIds == null || selectedIds.Contains(c.IdProduct)).ToList();

            // 🔹 Lấy thông tin sản phẩm và khuyến mãi
            var productIdsList = productsInCart.Select(c => c.IdProduct).ToList();
            var products = await _db.Products.Where(p => productIdsList.Contains(p.IdProduct)).ToListAsync();
            var activePromos = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => pp.Promotion.IsActive &&
                             pp.Promotion.StartDate <= DateTime.UtcNow &&
                             pp.Promotion.EndDate >= DateTime.UtcNow &&
                             productIdsList.Contains(pp.IdProduct))
                .ToListAsync();

            // 🔹 Tạo model view
            var model = new List<CartItemViewModel>();
            decimal total = 0;
            foreach (var item in productsInCart)
            {
                var product = products.FirstOrDefault(p => p.IdProduct == item.IdProduct);
                if (product == null) continue;

                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == product.IdProduct);
                decimal discountPercent = promo?.Promotion?.DiscountPercent ?? 0;
                decimal discountedPrice = product.Price * (1 - discountPercent / 100);

                model.Add(new CartItemViewModel
                {
                    Product = product,
                    Quantity = item.Quantity,
                    DiscountPercent = discountPercent
                });

                total += discountedPrice * item.Quantity;
            }

            ViewBag.Total = total;
            ViewBag.OrderCode = orderCode;
            // 🔹 Lấy ưu đãi thành viên hiện tại
            var rank = await _db.CustomerRankStats
                .Where(r => r.IdCustomer == customer.IdCustomer)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            decimal memberDiscountPercent = rank?.DiscountPercent ?? 0;
            ViewBag.MemberDiscount = memberDiscountPercent;

            // Tổng sau khi áp dụng khuyến mãi sản phẩm (chưa trừ rank)
            decimal subtotal = total;

            // Nếu có ưu đãi thành viên, áp dụng tiếp:
            decimal totalAfterRankDiscount = subtotal * (1 - memberDiscountPercent / 100);

            ViewBag.Total = totalAfterRankDiscount;
            ViewBag.TotalDiscount = subtotal - totalAfterRankDiscount; // tổng giảm
            ViewBag.OrderCode = orderCode;

            return View(model);
        }

        // -------------------------------
        // 📍 POST: Checkout theo yêu cầu (CÓ GIẢM GIÁ)
        // -------------------------------
        [HttpPost]
        public async Task<JsonResult> CheckoutAjax(
            string FullName, string Phone, string Address, string PaymentMethod)
        {
            var cart = GetCart();
            if (cart == null || !cart.Any())
                return Json(new { success = false, message = "Giỏ hàng trống!" });

            // 🔹 Lấy danh sách sản phẩm từ session
            var selectedIdsStr = HttpContext.Session.GetString("SelectedCartItems");
            if (string.IsNullOrEmpty(selectedIdsStr))
                return Json(new { success = false, message = "Không có sản phẩm nào được chọn!!!" });

            var selectedIds = selectedIdsStr.Split(',').Select(int.Parse).ToList();

            var selectedCart = cart.Where(c => selectedIds.Contains(c.IdProduct)).ToList();
            if (!selectedCart.Any())
                return Json(new { success = false, message = "Không có sản phẩm nào được chọn!" });

            // 🔹 Lấy thông tin user
            var userIdClaim = User.FindFirstValue("AppUserId");
            if (!int.TryParse(userIdClaim, out int idUser))
            {
                return Json(new { success = false, message = "Bạn chưa đăng nhập!" });
            }

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.IdUser == idUser);
            if (customer == null)
                return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng." });

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Phone) ||
                string.IsNullOrWhiteSpace(Address) ||
                string.IsNullOrWhiteSpace(PaymentMethod))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin thanh toán." });
            }

            // 🔹 Lấy lại mã đơn hàng
            string? orderCode = HttpContext.Session.GetString(ORDER_CODE_KEY);
            if (string.IsNullOrEmpty(orderCode))
                return Json(new { success = false, message = "Không tìm thấy mã đơn hàng. Vui lòng tải lại trang." });

            // 🔹 Lấy danh sách sản phẩm và khuyến mãi hiện hành
            var productIds = selectedCart.Select(c => c.IdProduct).ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.IdProduct)).ToListAsync();
            var activePromos = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => pp.Promotion.IsActive &&
                             pp.Promotion.StartDate <= DateTime.UtcNow &&
                             pp.Promotion.EndDate >= DateTime.UtcNow &&
                             productIds.Contains(pp.IdProduct))
                .ToListAsync();

            // 🔹 Lấy ưu đãi thành viên hiện tại
            var rank = await _db.CustomerRankStats
                .Where(r => r.IdCustomer == customer.IdCustomer)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            decimal memberDiscountPercent = rank?.DiscountPercent ?? 0;

            // 🔹 Tính tổng tiền có khuyến mãi
            decimal totalAmount = 0;
            foreach (var c in selectedCart)
            {
                var p = products.FirstOrDefault(x => x.IdProduct == c.IdProduct);
                if (p == null) continue;

                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                decimal discountPercent = promo?.Promotion?.DiscountPercent ?? 0;
                decimal discountedPrice = p.Price * (1 - discountPercent / 100);

                totalAmount += discountedPrice * c.Quantity;
            }

            // 🔹 Áp dụng thêm ưu đãi thành viên
            decimal memberDiscountAmount = totalAmount * (memberDiscountPercent / 100);
            totalAmount -= memberDiscountAmount;

            // 🔹 Lấy IdStore từ session (nếu có)
            int? idStore = HttpContext.Session.GetInt32("IdStore");
            if (idStore == null || idStore == 0)
            {
                // Nếu chưa có trong session → dùng chi nhánh mặc định (ví dụ Id = 1)
                idStore = 1;
                Console.WriteLine("⚠️ IdStore không có trong session → dùng mặc định = 1");
            }
            else
            {
                Console.WriteLine($"🏪 Đơn hàng thuộc chi nhánh IdStore = {idStore}");
            }
            bool IsPay = (PaymentMethod == "COD" ? false : true);

            // 🔹 Tạo đơn hàng
            var order = new Order
            {
                IdCustomer = customer.IdCustomer,
                IdStore = idStore.Value, // ✅ Gán IdStore
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                Status = "Đang xử lý",
                PaymentMethod = PaymentMethod,
                Pay = IsPay,
                Receiver = FullName,
                Phone = Phone,
                Address = Address,
                OrderCode = orderCode,
                IdEmployee = 0 //chưa có nhân viên xử lý; id=0
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 🔹 Thêm chi tiết đơn hàng (đã áp dụng giảm giá)
            foreach (var c in selectedCart)
            {
                var p = products.FirstOrDefault(x => x.IdProduct == c.IdProduct);
                if (p == null) continue;

                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                decimal discountPercent = promo?.Promotion?.DiscountPercent ?? 0;
                decimal discountedPrice = p.Price * (1 - discountPercent / 100);

                _db.OrderDetail.Add(new OrderDetail
                {
                    IdOrder = order.IdOrder,
                    IdProduct = p.IdProduct,
                    Quantity = c.Quantity,
                    UnitPrice = discountedPrice
                });
            }

            await _db.SaveChangesAsync();

            // ✅ Xoá các sản phẩm đã thanh toán khỏi giỏ hàng cookie
            cart.RemoveAll(c => selectedIds.Contains(c.IdProduct));
            SaveCart(cart);
            HttpContext.Session.Remove(ORDER_CODE_KEY);

            return Json(new
            {
                success = true,
                message = $"Cảm ơn {FullName}! Đơn hàng của bạn đã được lưu và đang được xử lý.",
                orderCode = orderCode,
                totalAmount = totalAmount
            });
        }
        // -------------------------------
        // 📍 POST: Checkout 1 sản phẩm (Áp dụng ưu đãi rank)
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckoutSingle(int IdProduct, int quantity = 1)
        {
            var cart = GetCart();

            // 🔹 Thêm sản phẩm vào giỏ
            var item = cart.FirstOrDefault(c => c.IdProduct == IdProduct);
            if (item != null)
                item.Quantity += quantity;
            else
                cart.Add(new CartItem { IdProduct = IdProduct, Quantity = quantity });

            SaveCart(cart);

            // 🔹 Lưu danh sách sản phẩm đã chọn vào session
            var selectedIds = new List<int> { IdProduct };
            HttpContext.Session.SetString("SelectedCartItems", string.Join(",", selectedIds));

            // 🔹 Chuyển hướng sang trang Checkout (GET)
            return RedirectToAction("Checkout", new { productIds = IdProduct });
        }

        [HttpGet]
        public async Task<JsonResult> GetCartTotals()
        {
            var cart = GetCart();
            var ids = cart.Select(c => c.IdProduct).ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.IdProduct)).ToListAsync();
            var activePromos = await _db.PromotionProducts.Include(pp => pp.Promotion)
                .Where(pp => ids.Contains(pp.IdProduct) && pp.Promotion.IsActive
                             && pp.Promotion.StartDate <= DateTime.UtcNow
                             && pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            decimal total = 0;
            var subtotals = new Dictionary<int, decimal>();
            foreach (var c in cart)
            {
                var p = products.FirstOrDefault(x => x.IdProduct == c.IdProduct);
                if (p == null) continue;
                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                var discount = promo?.Promotion?.DiscountPercent ?? 0;
                var price = p.Price * (1 - discount / 100);
                var sub = price * c.Quantity;
                subtotals[c.IdProduct] = sub;
                total += sub;
            }

            return Json(new { total, subtotals, cartCount = cart.Sum(c => c.Quantity) });
        }
    }
}
