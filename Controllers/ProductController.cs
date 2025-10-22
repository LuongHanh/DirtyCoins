// Controllers/ProductController.cs
using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DirtyCoins.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ProductController(ApplicationDbContext db) { _db = db; }

        // -------------------------------
        // 📍 Danh sách sản phẩm + tìm kiếm + lọc giá + danh mục
        // -------------------------------
        public async Task<IActionResult> Index(int? categoryId, string? q, string? priceRange)
        {
            ViewBag.Categories = await _db.Categories.ToListAsync();

            // ✅ Lấy chi nhánh hiện tại từ session
            var selectedStoreId = HttpContext.Session.GetInt32("SelectedStore");
            if (selectedStoreId == null)
            {
                // Nếu chưa chọn → lấy chi nhánh đầu tiên
                selectedStoreId = await _db.Stores.Select(s => s.IdStore).FirstOrDefaultAsync();
                HttpContext.Session.SetInt32("SelectedStore", selectedStoreId.Value);
            }

            var query = _db.Products
                .Include(p => p.Category)
                .Where(p => p.IdStore == selectedStoreId)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.IdCategory == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(p => p.Name.Contains(q));

            var allProducts = await query.ToListAsync();

            var activePromos = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Include(pp => pp.Product)
                .Where(pp =>
                    pp.Product.IdStore == selectedStoreId && // 🔹 chỉ lấy promo của chi nhánh hiện tại
                    pp.Promotion.IsActive &&
                    pp.Promotion.StartDate <= DateTime.UtcNow &&
                    pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            // Các khoảng giá cố định
            (decimal min, decimal max)? selectedRange = priceRange switch
            {
                "0-500" => (0, 500_000),
                "500-1000" => (500_000, 1_000_000),
                "1000-5000" => (1_000_000, 5_000_000),
                "5000+" => (5_000_000, decimal.MaxValue),
                _ => null
            };

            var products = allProducts.Select(p =>
            {
                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                var discount = promo?.Promotion?.DiscountPercent ?? 0;
                var discountedPrice = promo != null
                    ? Math.Round(p.Price * (1 - discount / 100m), 0)
                    : p.Price;

                return new DirtyCoins.ViewModels.ProductViewModel
                {
                    IdProduct = p.IdProduct,
                    Name = p.Name,
                    Image = p.Image,
                    Price = p.Price,
                    Category = p.Category,
                    DiscountPercent = discount,
                    DiscountedPrice = discountedPrice
                };
            }).ToList();

            ViewBag.SelectedPriceRange = priceRange;

            return View(products);
        }

        // -------------------------------
        // 📍 AJAX filter sản phẩm
        // -------------------------------
        [HttpGet]
        public async Task<IActionResult> FilterProducts(int? categoryId, string? priceRange, string? q)
        {
            // ✅ Lấy chi nhánh hiện tại từ session
            var selectedStoreId = HttpContext.Session.GetInt32("SelectedStore");
            if (selectedStoreId == null)
            {
                // Nếu chưa chọn → lấy chi nhánh đầu tiên
                selectedStoreId = await _db.Stores.Select(s => s.IdStore).FirstOrDefaultAsync();
                HttpContext.Session.SetInt32("SelectedStore", selectedStoreId.Value);
            }

            // 🔹 Truy vấn cơ bản
            var query = _db.Products
                .Include(p => p.Category)
                .Where(p => p.IdStore == selectedStoreId)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.IdCategory == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(p => p.Name.Contains(q));

            // 🔹 Load sản phẩm
            var allProducts = await query.ToListAsync();

            // 🔹 Khuyến mãi còn hiệu lực
            var activePromos = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Include(pp => pp.Product)
                .Where(pp =>
                    pp.Product.IdStore == selectedStoreId && // 🔹 chỉ lấy promo của chi nhánh hiện tại
                    pp.Promotion.IsActive &&
                    pp.Promotion.StartDate <= DateTime.UtcNow &&
                    pp.Promotion.EndDate >= DateTime.UtcNow)
                .ToListAsync();

            // 🔹 Ghép khuyến mãi
            var products = allProducts.Select(p =>
            {
                var promo = activePromos.FirstOrDefault(ap => ap.IdProduct == p.IdProduct);
                var discount = promo?.Promotion?.DiscountPercent ?? 0;
                var discountedPrice = promo != null
                    ? Math.Round(p.Price * (1 - discount / 100m), 0)
                    : p.Price;

                return new
                {
                    p.IdProduct,
                    p.Name,
                    p.Image,
                    p.Price,
                    p.Category,
                    DiscountPercent = discount,
                    DiscountedPrice = discountedPrice
                };
            }).ToList();

            // 🔹 Filter theo khoảng giá
            if (!string.IsNullOrEmpty(priceRange))
            {
                decimal min = 0, max = decimal.MaxValue;

                if (priceRange.Contains("-"))
                {
                    var parts = priceRange.Split('-');
                    decimal.TryParse(parts[0], out min);
                    decimal.TryParse(parts[1], out max);
                }
                else if (priceRange.EndsWith("+"))
                {
                    decimal.TryParse(priceRange.TrimEnd('+'), out min);
                    max = decimal.MaxValue;
                }

                products = products.Where(p => p.DiscountedPrice >= min * 1000 && p.DiscountedPrice <= max * 1000).ToList();
            }

            // 🔹 Convert sang ViewModel để partial view
            var productVMs = products.Select(p => new DirtyCoins.ViewModels.ProductViewModel
            {
                IdProduct = p.IdProduct,
                Name = p.Name,
                Image = p.Image,
                Price = p.Price,
                DiscountPercent = p.DiscountPercent,
                DiscountedPrice = p.DiscountedPrice
            }).ToList();

            return PartialView("_ProductListPartial", productVMs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages) // 🔹 load ảnh
                .FirstOrDefaultAsync(p => p.IdProduct == id);

            if (product == null) return NotFound();

            var promo = await _db.PromotionProducts
                .Include(pp => pp.Promotion)
                .Where(pp => pp.IdProduct == id &&
                             pp.Promotion.IsActive &&
                             pp.Promotion.StartDate <= DateTime.UtcNow &&
                             pp.Promotion.EndDate >= DateTime.UtcNow)
                .FirstOrDefaultAsync();

            decimal discountPercent = promo?.Promotion?.DiscountPercent ?? 0;
            decimal discountedPrice = discountPercent > 0
                ? Math.Round(product.Price * (1 - discountPercent / 100), 0)
                : product.Price;

            // 🔹 Load feedback + reply + customer
            var feedbacks = await _db.Feedbacks
                .Where(f => f.IdProduct == id)
                .Include(f => f.Customer)
                .Include(f => f.Replies)
                .OrderByDescending(f => f.Date)
                .Take(20)
                .ToListAsync();

            var vm = new ProductDetailViewModel
            {
                Product = product,
                Feedbacks = feedbacks.Select(f => new FeedbackViewModel
                {
                    IdFeedback = f.IdFeedback,
                    UserName = f.Customer?.FullName ?? "Người dùng",
                    Rating = f.Rating,
                    Content = f.Content,
                    LikeCount = f.LikeCount,
                    Replies = f.Replies.Select(r => new ReplyViewModel
                    {
                        IdReply = r.IdReply,
                        UserName = r.UserName,
                        ReplyContent = r.ReplyContent,
                        ReplyDate = r.ReplyDate,
                        IsStaff = r.IsStaff
                    }).ToList()
                }).ToList(),
                RelatedProducts = await _db.Products
                    .Where(p => p.IdCategory == product.IdCategory && p.IdProduct != id)
                    .Take(4)
                    .ToListAsync(),
                DiscountPercent = discountPercent,
                DiscountedPrice = discountedPrice
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFeedback([FromForm] int productId, [FromForm] int rating, [FromForm] string content)
        {
            try
            {
                var userIdClaim = User.FindFirst("AppUserId")?.Value;
                if (userIdClaim == null)
                    return Json(new { success = false, message = "Vui lòng đăng nhập để gửi đánh giá." });

                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.IdUser == int.Parse(userIdClaim));
                if (customer == null)
                    return Json(new { success = false, message = "Người dùng chưa có thông tin khách hàng." });

                var feedback = new Feedback
                {
                    IdProduct = productId,
                    IdCustomer = customer.IdCustomer,
                    Rating = rating,
                    Content = content,
                    Date = DateTime.UtcNow
                };

                _db.Feedbacks.Add(feedback);
                await _db.SaveChangesAsync();

                // Load đầy đủ dữ liệu để trả về ViewModel
                var feedbackVM = await _db.Feedbacks
                    .Where(f => f.IdFeedback == feedback.IdFeedback)
                    .Include(f => f.Customer)
                    .Select(f => new FeedbackViewModel
                    {
                        IdFeedback = f.IdFeedback,
                        UserName = f.Customer != null ? f.Customer.FullName : "Người dùng",
                        Rating = f.Rating,
                        Content = f.Content,
                        LikeCount = f.LikeCount,
                        Replies = new List<ReplyViewModel>()
                    }).FirstOrDefaultAsync();

                // trả JSON đầy đủ để JS render
                return Json(new
                {
                    success = true,
                    feedback = feedbackVM
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyFeedback(int feedbackId, string replyContent)
        {
            if (string.IsNullOrWhiteSpace(replyContent))
                return Json(new { success = false, message = "Vui lòng nhập nội dung phản hồi." });

            var feedback = await _db.Feedbacks.FindAsync(feedbackId);
            if (feedback == null)
                return Json(new { success = false, message = "Không tìm thấy đánh giá." });

            string userName;
            bool isStaff = false;

            if (User.IsInRole("Staff"))
            {
                userName = "Nhân viên DirtyCoins";
                isStaff = true;
            }
            else if (User.IsInRole("StoreOwner"))
            {
                userName = "StoreOwner";
            }
            else if (User.IsInRole("Director"))
            {
                userName = "Director";
            }
            else
            {
                var userIdClaim = User.FindFirst("AppUserId")?.Value;
                if (userIdClaim == null)
                    return Json(new { success = false, message = "Vui lòng đăng nhập để phản hồi." });

                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.IdUser == int.Parse(userIdClaim));
                userName = customer?.FullName ?? "Người dùng";
            }

            var reply = new FeedbackReply
            {
                IdFeedback = feedbackId,
                ReplyContent = replyContent.Trim(),
                UserName = userName,
                IsStaff = isStaff
            };

            _db.FeedbackReplies.Add(reply);
            await _db.SaveChangesAsync();

            // Reload để chắc chắn Date được set
            var replyVM = await _db.FeedbackReplies
                .Where(r => r.IdReply == reply.IdReply)
                .Select(r => new ReplyViewModel
                {
                    IdReply = r.IdReply,
                    UserName = r.UserName,
                    ReplyContent = r.ReplyContent,
                    ReplyDate = r.ReplyDate,
                    IsStaff = r.IsStaff
                }).FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                reply = replyVM
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LikeFeedback(int feedbackId)
        {
            var userId = User.FindFirstValue("AppUserId");
            if (userId == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });

            // Kiểm tra đã like chưa
            bool hasLiked = await _db.FeedbackLikes.AnyAsync(l => l.IdFeedback == feedbackId && l.IdUser == int.Parse(userId));
            if (hasLiked)
                return Json(new { success = false, message = "Bạn đã thích đánh giá này." });

            var like = new FeedbackLike
            {
                IdFeedback = feedbackId,
                IdUser = int.Parse(userId)
            };
            _db.FeedbackLikes.Add(like);

            // Tăng count trong Feedback
            var feedback = await _db.Feedbacks.FindAsync(feedbackId);
            feedback.LikeCount++;
            await _db.SaveChangesAsync();

            return Json(new { success = true, likeCount = feedback.LikeCount });
        }
    }
}
