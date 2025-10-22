using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Security;
using DirtyCoins.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DirtyCoins.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GeocodingService _geo;
        private readonly SystemLogService _logService;
        public AdminController(ApplicationDbContext context, GeocodingService geo, SystemLogService logService)
        {
            _context = context;
            _geo = geo;
            _logService = logService;
        }

        // -------------------------------
        // 📍 Dashboard
        // -------------------------------
        public async Task<IActionResult> Dashboard()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStores = await _context.Stores.CountAsync();
            var totalEmployees = await _context.Employee.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalAdmins = await _context.Users.CountAsync(u => u.Role == "Admin");
            var totalDirectors = await _context.Users.CountAsync(u => u.Role == "Director");

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalStores = totalStores;
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalDirectors = totalDirectors;

            return View();
        }

        // 🛠️ Model tạm cho request JSON
        public class MaintenanceRequest
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string? Reason { get; set; }
            public bool IsImportant { get; set; }
        }
        

        [HttpGet("/api/admin/online-stats")]
        public IActionResult GetOnlineStats()
        {
            var totalOnline = _context.Users.Count(u => u.IsOnline == true);

            var byRole = _context.Users
                .Where(u => u.IsOnline == true)
                .GroupBy(u => u.Role)
                .Select(g => new {
                    Role = g.Key,
                    Count = g.Count()
                })
                .ToList();

            return Json(new
            {
                TotalOnline = totalOnline,
                ByRole = byRole
            });
        }

        // -------------------------------
        // 📍 Quản lý người dùng
        // -------------------------------
        public async Task<IActionResult> UserManagement()
        {
            // Lấy 50 khách hàng mới nhất
            var customers = await _context.Users
                .Where(u => u.Role.ToLower() == "customer")
                .OrderByDescending(u => u.CreatedAt)
                .Take(50)
                .ToListAsync();

            // Lấy tất cả người dùng còn lại
            var others = await _context.Users
                .Where(u => u.Role.ToLower() != "customer")
                .OrderBy(u => u.Role)
                .ToListAsync();

            // Gửi 2 danh sách qua ViewBag
            ViewBag.Customers = customers;
            ViewBag.Others = others;

            ViewBag.Stores = await _context.Stores
                .Select(s => new { s.IdStore, s.StoreName })
                .ToListAsync();

            return View();
        }

        // -------------------------------
        // 📍 GET: CreateUser
        // -------------------------------
        [HttpGet]
        public IActionResult CreateUser()
        {
            ViewBag.Roles = new List<string> { "Staff", "StoreOwner", "Director", "Admin" };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
                string username,
                string email,
                string password,
                string role,
                string fullName,
                string phone,
                string position,
                int? idStore,
                string storeName,
                string storeAddress,
                string storeOwner)
         {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role) || string.IsNullOrEmpty(password))
                {
                    TempData["Error"] = "Vui lòng nhập đầy đủ thông tin bắt buộc!";
                    return RedirectToAction("UserManagement");
                }

                // ✅ Hash mật khẩu
                var passwordHash = PasswordHasherWithFixedSalt.HashPassword(password);

                var newUser = new User
                {
                    Username = username.Trim(),
                    Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower(),
                    Password = passwordHash,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                int idUser = newUser.IdUser;

                // 🔹 Tạo bản ghi phụ theo vai trò
                switch (role.ToLower())
                {
                    case "staff":
                        if (idStore == null)
                        {
                            TempData["Error"] = "Nhân viên phải thuộc một cửa hàng!";
                            return RedirectToAction("UserManagement");
                        }

                        var emp = new Employee
                        {
                            IdUser = idUser,
                            FullName = fullName,
                            Position = position,
                            HiredDate = DateTime.UtcNow,
                            IdStore = idStore.Value
                        };
                        _context.Employee.Add(emp);
                        break;

                    case "storeowner":
                        var store = new Store
                        {
                            StoreName = storeName,
                            Address = storeAddress,
                            StoreOwner = storeOwner,
                            Email = email,
                            Phone = phone,
                            IdUser = idUser
                        };
                        _context.Stores.Add(store);
                        await _context.SaveChangesAsync(); // 🔸 lưu tạm để có IdStore

                        try
                        {
                            string addressNormalized = store.Address.Trim();
                            if (!addressNormalized.ToLower().Contains("việt nam") && !addressNormalized.ToLower().Contains("vietnam"))
                                addressNormalized += ", Việt Nam";

                            var geoService = HttpContext.RequestServices.GetRequiredService<GeocodingService>();
                            var (lat, lon) = await geoService.GetCoordinatesAsync(addressNormalized);
                            if (lat != 0 && lon != 0)
                            {
                                store.Latitude = lat;
                                store.Longitude = lon;
                                await _context.SaveChangesAsync();
                                Console.WriteLine($"🌍 Đã cập nhật toạ độ cho cửa hàng {store.StoreName}: {lat}, {lon}");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Không tìm thấy toạ độ cho cửa hàng: {store.StoreName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GEO-ERROR] {ex.Message}");
                        }
                        break;

                    case "director":
                        var dir = new Director
                        {
                            IdUser = idUser,
                            FullName = fullName,
                            Phone = phone,
                            Email = email,
                            StartDate = DateTime.UtcNow
                        };
                        _context.Directors.Add(dir);
                        break;

                    case "admin":
                        break;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Tạo tài khoản {role} thành công!";
                await _logService.LogAsync(idUser, $"{fullName} đã tại tài khoản thành công với vai trò {role}");
                return RedirectToAction("UserManagement");
            }
            catch (DbUpdateException ex)
            {
                var inner = ex.InnerException?.Message ?? ex.GetBaseException().Message;
                Console.WriteLine($"[DB ERROR] {inner}");
                TempData["Error"] = "❌ Lỗi khi lưu dữ liệu: " + inner;
                return RedirectToAction("UserManagement");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ Lỗi khác: " + ex.Message;
                return RedirectToAction("UserManagement");
            }
        }

        // -------------------------------
        // 📍 Nhật ký hệ thống (SystemLog)
        // -------------------------------
        public async Task<IActionResult> SystemLog(string searchAction, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.SystemLogs
                .Include(l => l.User)
                .AsQueryable();

            // Lọc theo Action nếu có nhập
            if (!string.IsNullOrEmpty(searchAction))
            {
                query = query.Where(l => l.Action.Contains(searchAction));
            }

            // Lọc theo khoảng thời gian nếu có nhập
            if (fromDate.HasValue)
            {
                query = query.Where(l => l.TimeStamp >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                // Thêm 1 ngày để bao gồm cả ngày kết thúc
                query = query.Where(l => l.TimeStamp <= toDate.Value.AddDays(1).AddTicks(-1));
            }

            var logs = await query
                .OrderByDescending(l => l.TimeStamp)
                .Take(100)
                .ToListAsync();

            // Giữ lại giá trị tìm kiếm để hiển thị trong View
            ViewBag.SearchAction = searchAction;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }

        // -------------------------------
        // 📍 Link truy cập của Admin khi hệ thống đang bảo trì
        // -------------------------------
        [HttpGet("/admin/override-maintenance")]
        public IActionResult OverrideMaintenance()
        {
            return View("Dashboard"); // Trang riêng cho admin
        }
    }
}
