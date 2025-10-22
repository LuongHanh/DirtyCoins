using DirtyCoins.Data;
using DirtyCoins.Hubs;
using DirtyCoins.Models;
using DirtyCoins.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<CategoryHub> _hubContext;
        private readonly SystemLogService _logService;
        public StaffController(ApplicationDbContext context, IHubContext<CategoryHub> hubContext, SystemLogService logService)
        {
            _context = context;
            _hubContext = hubContext;
            _logService = logService;
        }

        // Lấy IdUser từ claim đăng nhập
        private int? GetCurrentUserId()
        {
            string? idStr = User.FindFirstValue("AppUserId")
                            ?? User.FindFirstValue("IdUser");
            return int.TryParse(idStr, out int id) ? id : null;
        }

        // -------------------------------
        // 📍 Dashboard
        // -------------------------------
        public IActionResult Dashboard()
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            var employee = _context.Employee
                .Include(e => e.User)
                .Include(e => e.Store)
                .FirstOrDefault(e => e.IdUser == idUser.Value);

            if (employee == null)
                return RedirectToAction("Login", "Account");

            // 🔹 Thống kê cơ bản
            ViewBag.TotalProducts = _context.Products.Count(p => p.IdStore == employee.IdStore);
            ViewBag.TotalOrders = _context.Orders.Count(o => o.IdStore == employee.IdStore);
            ViewBag.TotalCustomers = _context.Orders
                .Where(o => o.IdStore == employee.IdStore)
                .Select(o => o.IdCustomer)
                .Distinct()
                .Count();
            ViewBag.PendingFeedbacks = _context.Contacts.Count(f => !f.IsHandled && f.IdStore == employee.IdStore);

            // 🔹 Lấy danh sách Contact (liệt kê mới nhất 10 liên hệ)
            var contacts = _context.Contacts
                .Where(c => c.IdStore == employee.IdStore) // 🔹 chỉ lấy liên hệ thuộc cửa hàng của nhân viên
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .ToList();

            ViewBag.Contacts = contacts;

            return View(employee); // Views/Staff/Dashboard.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HandleContact(int idContact)
        {
            var contact = _context.Contacts.FirstOrDefault(c => c.IdContact == idContact);
            if (contact == null)
                return Json(new { success = false, message = "Không tìm thấy liên hệ." });

            contact.IsHandled = true;
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // -------------------------------
        // 📍 Quản lý sản phẩm
        // -------------------------------
        [HttpGet]
        public IActionResult ManageProducts()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.IdProduct)
                .ToList();
            var promotions = _context.Promotions.ToList();
            var categories = _context.Categories.ToList();
            ViewBag.Categories = categories;
            ViewBag.Promotions = promotions;
            return View(products);
        }

        // -------------------------------
        // 📍 Thêm sản phẩm mới
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddProduct(Product model, IFormFile ImageFile)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            // ✅ Bỏ qua check ModelState.IsValid, vì mình tự xử lý hợp lệ
            if (ImageFile == null || ImageFile.Length == 0)
            {
                return Json(new { success = false, message = "⚠️ Vui lòng chọn ảnh sản phẩm." });
            }

            // ✅ Lưu ảnh
            var fileName = Path.GetFileName(ImageFile.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/products");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                ImageFile.CopyTo(stream);
            }

            // ✅ Gán thủ công các trường cần thiết
            model.Image = "/images/products/" + fileName;
            model.Feedbacks = new List<Feedback>();
            model.OrderDetails = new List<OrderDetail>();

            _context.Products.Add(model);
            _context.SaveChanges();
            _logService.LogAsync(employee.IdEmployee, $"Sản phẩm mới đã được thêm thành công");
            return Json(new { success = true, message = "✅ Thêm sản phẩm thành công!" });
        }

        // -------------------------------
        // 📍 Cập nhật sản phẩm
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProduct([FromForm] Product updated)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            var product = _context.Products.FirstOrDefault(p => p.IdProduct == updated.IdProduct);
            if (product == null) return NotFound();

            product.Name = updated.Name;
            product.Price = updated.Price;
            product.Quantity = updated.Quantity;
            product.IdCategory = updated.IdCategory;

            _context.SaveChanges();
            _logService.LogAsync(employee.IdEmployee, $"{product.Name} đã được cập nhật thành công");
            return Ok(new { success = true, message = "Cập nhật sản phẩm thành công!" });
        }

        // -------------------------------
        // 📍 Xóa sản phẩm
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteProduct(int id)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            var product = _context.Products.FirstOrDefault(p => p.IdProduct == id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            _context.SaveChanges();
            _logService.LogAsync(employee.IdEmployee, $"Sản phẩm đã được xoá thành công");

            return Ok(new { success = true, message = "Đã xóa sản phẩm!" });
        }

        // -------------------------------
        // 📍 Trang quản lý Category
        // -------------------------------
        public IActionResult Categories()
        {
            var categories = _context.Categories.ToList();
            return View(categories);
        }

        // -------------------------------
        // 📍 Thêm Category mới
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCategory([FromBody] Category model)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "❌ Dữ liệu không hợp lệ.", errors });
            }

            _context.Categories.Add(model);
            _context.SaveChanges();

            _hubContext.Clients.All.SendAsync("ReloadCategories");
            _logService.LogAsync(employee.IdEmployee, $"Danh mục mới đã được thêm thành công");

            return Json(new { success = true, message = "✅ Thêm loại sản phẩm thành công!" });
        }

        // -------------------------------
        // 📍 Cập nhật Category
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCategory(Category model)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            var existing = _context.Categories.Find(model.IdCategory);
            if (existing == null)
                return Json(new { success = false, message = "Không tìm thấy loại sản phẩm." });

            existing.Name = model.Name;
            existing.Description = model.Description;
            _context.SaveChanges();

            _hubContext.Clients.All.SendAsync("ReloadCategories");
            _logService.LogAsync(employee.IdEmployee, $"Danh mục đã được cập nhật thành công");

            return Json(new { success = true, message = "✅ Cập nhật thành công!" });
        }

        // -------------------------------
        // 📍 Xóa Category
        // -------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            var cat = _context.Categories.Find(id);
            if (cat == null)
                return Json(new { success = false, message = "Không tìm thấy loại sản phẩm." });

            _context.Categories.Remove(cat);
            _context.SaveChanges();

            _hubContext.Clients.All.SendAsync("ReloadCategories");
            _logService.LogAsync(employee.IdEmployee, $"Danh mục đã được xoá thành công");

            return Json(new { success = true, message = "🗑️ Xóa thành công!" });
        }

        // 📍 1. Xem danh sách + lọc + tìm kiếm đơn hàng
        public IActionResult ManageOrders(string? status, string? searchName, string? receiver, DateTime? date, string? orderCode)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            if (employee == null)
                return RedirectToAction("Dashboard", "Store"); // hoặc lỗi phù hợp

            var idStore = employee.IdStore;

            // Danh sách trạng thái
            ViewBag.StatusList = new List<string>
            {
                "Đang xử lý",
                "Chờ giao hàng",
                "Đang giao hàng",
                "Giao hàng thành công",
                "Đã hủy"
            };

            // ✅ Chỉ lấy đơn hàng thuộc cửa hàng nhân viên đang làm việc
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Where(o => o.IdStore == idStore) // 👈 Lọc theo cửa hàng
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
                ViewBag.SelectedStatus = status;
            }

            // 🔍 Tìm theo mã đơn hàng
            if (!string.IsNullOrEmpty(orderCode))
            {
                query = query.Where(o => o.OrderCode.Contains(orderCode));
                ViewBag.OrderCode = orderCode;
            }

            // Tìm kiếm theo khách hàng
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(o => o.Customer.FullName.Contains(searchName));
                ViewBag.SearchName = searchName;
            }

            // Tìm kiếm theo người nhận
            if (!string.IsNullOrEmpty(receiver))
            {
                query = query.Where(o => o.Receiver.Contains(receiver));
                ViewBag.Receiver = receiver;
            }

            // Tìm kiếm theo ngày đặt
            if (date != null)
            {
                var targetDate = date.Value.Date;
                query = query.Where(o => o.OrderDate.Date == targetDate);
                ViewBag.OrderDate = targetDate.ToString("yyyy-MM-dd");
            }

            var list = query.OrderByDescending(o => o.OrderDate).ToList();
            return View(list);
        }

        // 📍 2. Xem chi tiết đơn hàng (và in bill)
        public IActionResult OrderDetails(int id)
        {
            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefault(o => o.IdOrder == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 📍 3. Cập nhật trạng thái đơn hàng (từng đơn)
        [HttpPost]
        public IActionResult UpdateOrderStatus(int id, string status)
        {
            var order = _context.Orders.FirstOrDefault(o => o.IdOrder == id);
            if (order == null) return NotFound();

            if (order.Status == "Đã hủy")
            {
                TempData["Message"] = $"❌ Không thể cập nhật đơn #{id} vì đã bị hủy.";
                return RedirectToAction(nameof(ManageOrders));
            }

            // 1️⃣ Lấy IdUser từ Session
            var userIdClaim = User.FindFirstValue("AppUserId");
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Json(new { success = false, message = "Bạn chưa đăng nhập!" });
            }

            // 2️⃣ Lấy IdEmployee từ bảng Employee dựa trên IdUser
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == userId);
            if (employee == null)
            {
                TempData["Message"] = "❌ Nhân viên chưa được đăng ký trong hệ thống.";
                return RedirectToAction(nameof(ManageOrders));
            }

            // 3️⃣ Cập nhật Status và IdEmployee
            order.Status = status;
            order.IdEmployee = employee.IdEmployee;
            _context.SaveChanges();
            TempData["Message"] = $"✅ Đơn #{order.OrderCode} đã cập nhật sang “{status}”.";
            _logService.LogAsync(employee.IdEmployee, $"Đơn hàng của khách hàng mã {order.IdCustomer} đã được cập nhật thành công");
            return RedirectToAction(nameof(ManageOrders));
        }

        // 📍 4. Cập nhật hàng loạt
        [HttpPost]
        public IActionResult BulkUpdate(string actionType)
        {
            // ✅ Danh sách hành động hợp lệ
            var validActions = new[] { "processing_to_pending", "pending_to_shipping", "rollback_one_step" };
            if (!validActions.Contains(actionType))
            {
                TempData["Message"] = "⚠ Hành động không hợp lệ.";
                return RedirectToAction(nameof(ManageOrders));
            }

            // ✅ Lấy thông tin người dùng hiện tại
            var currentUser = User?.Identity?.Name ?? "system";
            var idUser = GetCurrentUserId(); // tự mày viết hoặc lấy từ session
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Lấy nhân viên & cửa hàng của họ
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            if (employee == null)
            {
                TempData["Message"] = "⚠ Không xác định được nhân viên hiện tại.";
                return RedirectToAction(nameof(ManageOrders));
            }

            var idStore = employee.IdStore;
            int count = 0;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // ================= ROLLBACK =================
                if (actionType == "rollback_one_step")
                {
                    var lastOp = _context.BulkOperations
                        .Where(o => !o.RolledBack && o.PerformedBy == currentUser)
                        .OrderByDescending(o => o.PerformedAt)
                        .FirstOrDefault();

                    if (lastOp == null)
                    {
                        TempData["Message"] = "⚠ Không có lịch sử cập nhật nào để hoàn lại.";
                        return RedirectToAction(nameof(ManageOrders));
                    }

                    var items = _context.BulkOperationItems
                        .Where(i => i.BulkOperationId == lastOp.Id)
                        .ToList();

                    foreach (var item in items)
                    {
                        var order = _context.Orders
                            .FirstOrDefault(o => o.IdOrder == item.OrderId && o.IdStore == idStore);

                        if (order == null) continue;
                        order.Status = item.OldStatus;
                        order.IdEmployee = 0; // ✅ rollback: reset lại
                        count++;
                    }

                    lastOp.RolledBack = true;
                    lastOp.ActionType += "_rollback";
                    _context.SaveChanges();

                    transaction.Commit();
                    _logService.LogAsync(employee.IdEmployee, $"NV mã {employee.IdEmployee} vừa hoàn lại trạng thái {count} đơn hàng");
                    TempData["Message"] = $"↩ Đã hoàn lại {count} đơn hàng của cửa hàng {idStore}.";
                    return RedirectToAction(nameof(ManageOrders));
                }

                // ================= FORWARD =================
                var op = new BulkOperation
                {
                    ActionType = actionType,
                    PerformedBy = currentUser,
                    PerformedAt = DateTime.UtcNow,
                    RolledBack = false
                };
                _context.BulkOperations.Add(op);
                _context.SaveChanges();

                List<Order> changedOrders = new();

                if (actionType == "processing_to_pending")
                {
                    changedOrders = _context.Orders
                        .Where(o => o.Status == "Đang xử lý" && o.IdStore == idStore)
                        .ToList();

                    foreach (var o in changedOrders)
                    {
                        _context.BulkOperationItems.Add(new BulkOperationItem
                        {
                            BulkOperationId = op.Id,
                            OrderId = o.IdOrder,
                            OldStatus = o.Status,
                            NewStatus = "Chờ giao hàng"
                        });
                        o.Status = "Chờ giao hàng";
                        // ✅ Gán IdEmployee người thao tác
                        o.IdEmployee = employee.IdEmployee;
                    }
                }
                else if (actionType == "pending_to_shipping")
                {
                    changedOrders = _context.Orders
                        .Where(o => o.Status == "Chờ giao hàng" && o.IdStore == idStore)
                        .ToList();

                    foreach (var o in changedOrders)
                    {
                        _context.BulkOperationItems.Add(new BulkOperationItem
                        {
                            BulkOperationId = op.Id,
                            OrderId = o.IdOrder,
                            OldStatus = o.Status,
                            NewStatus = "Đang giao hàng"
                        });
                        o.Status = "Đang giao hàng";
                        // ✅ Gán IdEmployee người thao tác
                        o.IdEmployee = employee.IdEmployee;
                    }
                }

                count = changedOrders.Count;
                if (count > 0)
                {
                    _context.SaveChanges();
                    transaction.Commit();
                    TempData["Message"] = $"✅ Đã cập nhật {count} đơn hàng của cửa hàng {idStore} (Operation ID: {op.Id}).";
                    _logService.LogAsync(employee.IdEmployee, $"NV mã {employee.IdEmployee} vừa cập nhật trạng thái {count} đơn hàng");
                }
                else
                {
                    _context.BulkOperations.Remove(op);
                    _context.SaveChanges();
                    TempData["Message"] = $"⚠ Không có đơn hàng phù hợp để cập nhật trong cửa hàng {idStore}.";
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Message"] = $"❌ Lỗi khi lưu: {ex.InnerException?.Message ?? ex.Message}";
            }

            return RedirectToAction(nameof(ManageOrders));
        }


        [HttpPost]
        public IActionResult RollbackBulk(int opId)
        {
            // ✅ Lấy IdUser hiện tại (tùy vào hệ thống login của mày)
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // ✅ Tìm nhân viên tương ứng với user đó
            var employee = _context.Employee.FirstOrDefault(e => e.IdUser == idUser.Value);
            var op = _context.BulkOperations
                .Include(b => b.Items)
                .FirstOrDefault(b => b.Id == opId);

            if (op == null)
            {
                TempData["Message"] = "Không tìm thấy thao tác hàng loạt.";
                return RedirectToAction(nameof(ManageOrders));
            }
            if (op.RolledBack)
            {
                TempData["Message"] = "Thao tác này đã được hoàn lại trước đó.";
                return RedirectToAction(nameof(ManageOrders));
            }

            int count = 0;
            foreach (var item in op.Items)
            {
                var order = _context.Orders.FirstOrDefault(o => o.IdOrder == item.OrderId);
                if (order == null) continue;

                // CHỈ rollback nếu hiện trạng hiện tại khác OldStatus (optional decision)
                // Nếu muốn ép luôn rollback, comment condition.
                if (order.Status == item.NewStatus)
                {
                    order.Status = item.OldStatus;
                    count++;
                }
                else
                {
                    // Nếu order đã bị thay đổi khác (ví dụ bị sửa tay sau bulk), ta có 2 lựa chọn:
                    // 1) Bỏ qua (an toàn) — như hiện tại.
                    // 2) Ghi log cảnh báo — tùy mày.
                }
            }

            if (count > 0)
            {
                op.RolledBack = true;
                _context.SaveChanges();
                TempData["Message"] = $"↩️ Đã hoàn lại {count} đơn (Operation {op.Id}).";
            }
            else
            {
                TempData["Message"] = "Không có đơn nào phù hợp để hoàn lại (có thể đã bị chỉnh sửa sau đó).";
            }

            return RedirectToAction(nameof(ManageOrders));
        }

        // 📍 5. Xuất tất cả bill “Đang xử lý”
        public IActionResult PrintAllBills()
        {
            var idUser = GetCurrentUserId(); // 🔹 hàm lấy id user hiện tại (đã có sẵn ở nhiều controller khác)
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // 🔹 Lấy nhân viên và cửa hàng của họ
            var employee = _context.Employee
                .Include(e => e.Store)
                .FirstOrDefault(e => e.IdUser == idUser.Value);

            if (employee == null)
                return Forbid(); // hoặc Redirect đến trang lỗi / thông báo

            var store = employee.Store;

            // 🔹 Lấy danh sách đơn hàng cần in
            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Where(o => o.Status == "Đang xử lý")
                .OrderBy(o => o.OrderDate)
                .ToList();

            // 🔹 Truyền cả store + orders sang view
            ViewBag.Store = store;
            return View("Bills", orders);
        }

        public IActionResult PrintBill(int id)
        {
            var idUser = GetCurrentUserId();
            if (!idUser.HasValue)
                return RedirectToAction("Login", "Account");

            // 🔹 Lấy nhân viên hiện tại và cửa hàng của họ
            var employee = _context.Employee
                .Include(e => e.Store)
                .FirstOrDefault(e => e.IdUser == idUser.Value);

            if (employee == null)
                return Forbid();

            var store = employee.Store;
            ViewBag.Store = store;

            // 🔹 Lấy đơn hàng cần in
            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefault(o => o.IdOrder == id);

            if (order == null)
                return NotFound();

            return View("OrderDetails", order); // View riêng cho bill lẻ
        }

        // -------------------------------
        // 📍 Quản lý thành viên
        // -------------------------------
        public IActionResult ManageCustomers()
        {
            var stats = _context.CustomerRankStats
                .Include(c => c.Customer)
                .OrderByDescending(c => c.TotalSpent)
                .ToList();

            return View(stats);
        }
        public IActionResult ExportCustomersToExcel()
        {
            var stats = _context.CustomerRankStats
                .Include(c => c.Customer)
                .OrderByDescending(c => c.TotalSpent)
                .ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("KhachHang");

            // Tiêu đề
            ws.Cells["A1:H1"].Merge = true;
            ws.Cells["A1"].Value = "THỐNG KÊ KHÁCH HÀNG THEO THÁNG";
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Size = 16;
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Header
            string[] headers = { "#", "Họ tên", "Email", "SĐT", "Số đơn", "Tổng chi tiêu", "Xếp hạng", "Ưu đãi (%)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[3, i + 1].Value = headers[i];
                ws.Cells[3, i + 1].Style.Font.Bold = true;
                ws.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                ws.Cells[3, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Dữ liệu
            int row = 4;
            int index = 1;
            foreach (var c in stats)
            {
                ws.Cells[row, 1].Value = index++;
                ws.Cells[row, 2].Value = c.Customer.FullName;
                ws.Cells[row, 3].Value = c.Customer.Address;
                ws.Cells[row, 4].Value = c.Customer.Phone;
                ws.Cells[row, 5].Value = c.OrderCount;
                ws.Cells[row, 6].Value = c.TotalSpent;
                ws.Cells[row, 6].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 7].Value = c.RankName;
                ws.Cells[row, 8].Value = c.DiscountPercent;
                row++;
            }

            // Tự động canh cột
            ws.Cells.AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            string fileName = $"ThongKeKhachHang_{DateTime.Now:yyyy_MM}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPromotion([FromForm] Promotion promo)
        {
            try
            {
                if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

                _context.Promotions.Add(promo);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ Thêm khuyến mãi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePromotion(int IdPromotion, string PromotionName, string DescriptionP,
            decimal DiscountPercent, DateTime StartDate, DateTime EndDate, bool IsActive)
        {
            var promo = await _context.Promotions.FindAsync(IdPromotion);
            if (promo == null)
                return Json(new { success = false, message = "Không tìm thấy khuyến mãi!" });

            promo.PromotionName = PromotionName;
            promo.DescriptionP = DescriptionP;
            promo.DiscountPercent = DiscountPercent;
            promo.StartDate = StartDate;
            promo.EndDate = EndDate;
            promo.IsActive = IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "✅ Cập nhật khuyến mãi thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null)
                return Json(new { success = false, message = "Không tìm thấy khuyến mãi!" });

            _context.Promotions.Remove(promo);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "🗑️ Đã xóa khuyến mãi!" });
        }
    }
}
