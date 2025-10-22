using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Claims;
using DirtyCoins.Services;

namespace DirtyCoins.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher = new();
        private readonly GeocodingService _geo;
        private readonly SystemLogService _logService;

        public AccountController(ApplicationDbContext db, GeocodingService geo, SystemLogService logService)
        {
            _db = db;
            _geo = geo;
            _logService = logService;
        }

        // 🔹 Hiển thị trang đăng nhập
        [HttpGet]
        public IActionResult Login(string? returnUrl = "/")
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // 🔹 Xử lý đăng nhập nội bộ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usernameOrEmail, string password, string? returnUrl = "/")
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            // ✅ Cho phép nhập username hoặc email
            // Email chưa dùng được
            var usernameEmail = usernameOrEmail?.Trim().ToLower();
            var user = _db.Users.FirstOrDefault(u =>
                u.Username == usernameEmail || u.Email == usernameEmail);

            if (user == null)
            {
                ViewBag.Error = $"Tên đăng nhập / Email hoặc mật khẩu không đúng./// Input: {usernameEmail}";
                return View();
            }

            if (user.IsActive == false)
            {
                ViewBag.Error = $"Tài khoản {usernameEmail} đã bị khoá!</br>"
                    +"Mọi thắc mắc vui lòng liên hệ quản trị viên.";
                return View();
            }

            var normalizeHash = PasswordHasherWithFixedSalt.HashPassword(password);
            bool verify = normalizeHash == user.Password;
            if (verify == false)
            {
                ViewBag.Error = "Tên đăng nhập / Email hoặc mật khẩu không đúng."+
                    $"Input password: {password} /// Hash DB: {user.Password} "+
                    $"Verify: {verify} /// Hash runtime: {normalizeHash}";
                return View();
            }

            // 🔹 Claim cơ bản (dùng cho mọi vai trò)
            var claims = new List<Claim>
            {
                new("AppUserId", user.IdUser.ToString()),
                new(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email ?? ""),
                new(ClaimTypes.Role, user.Role ?? "")
            };

            // 🔹 Thêm thông tin tĩnh theo từng vai trò
            switch (user.Role)
            {
                case "Customer":
                    var customer = _db.Customers.FirstOrDefault(c => c.IdUser == user.IdUser);
                    if (customer != null)
                    {
                        claims.Add(new Claim("FullName", customer.FullName ?? ""));
                        claims.Add(new Claim("Address", customer.Address ?? ""));
                        claims.Add(new Claim("Phone", customer.Phone ?? ""));
                    }
                    break;

                case "Staff":
                    var employee = _db.Employee.FirstOrDefault(e => e.IdUser == user.IdUser);
                    if (employee != null)
                    {
                        claims.Add(new Claim("FullName", employee.FullName ?? ""));
                        claims.Add(new Claim("Position", employee.Position ?? ""));
                        claims.Add(new Claim("IdStore", employee.IdStore.ToString()));
                    }
                    break;

                case "StoreOwner":
                    var store = _db.Stores.FirstOrDefault(s => s.IdUser == user.IdUser);
                    if (store != null)
                    {
                        claims.Add(new Claim("StoreName", store.StoreName ?? ""));
                        claims.Add(new Claim("FullName", store.StoreOwner ?? ""));
                        claims.Add(new Claim("EmailStore", store.Email ?? ""));
                        claims.Add(new Claim("PhoneStore", store.Phone ?? ""));
                        claims.Add(new Claim("AddressStore", store.Address ?? ""));
                        claims.Add(new Claim("IdStore", store.IdStore.ToString()));
                    }
                    break;

                case "Director":
                    // Giám đốc hệ thống: thêm thông tin tổng quan (nếu có)
                    var director = _db.Directors.FirstOrDefault(d => d.IdUser == user.IdUser);
                    if (director != null)
                    {
                        claims.Add(new Claim("Position", "Giám đốc hệ thống"));
                        claims.Add(new Claim("FullName", director.FullName ?? ""));
                        claims.Add(new Claim("Department", "Toàn bộ chuỗi cửa hàng"));
                    }
                    break;

                case "Admin":
                    // Quản trị viên hệ thống
                    claims.Add(new Claim("FullName", "Admin"));
                    claims.Add(new Claim("PermissionLevel", "Root"));
                    break;
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            user.IsOnline = true;
            user.LastActive = DateTime.UtcNow;
            _db.SaveChanges();

            // ✅ Điều hướng theo vai trò khác
            return user.Role switch
            {
                "Customer" => RedirectToAction("Dashboard", "Customer"),
                "Staff" => RedirectToAction("Dashboard", "Staff"),
                "StoreOwner" => RedirectToAction("Dashboard", "Store"),
                "Director" => RedirectToAction("Dashboard", "Director"),
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        // 🔹 Trang đăng ký (GET)
        [HttpGet]
        public IActionResult Register() => View();

        // 🔹 Xử lý đăng ký (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullname, string address, string phone, string username, string email, string password)
        {
            // --- 1️⃣ Kiểm tra dữ liệu cơ bản ---
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(address)
                || string.IsNullOrWhiteSpace(phone))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin bắt buộc.";
                return View();
            }

            var usernameNormalized = username.Trim();
            var emailNormalized = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower();

            if (_db.Users.Any(u => u.Username == usernameNormalized))
            {
                ViewBag.Error = "Tên đăng nhập đã tồn tại.";
                return View();
            }

            if (!string.IsNullOrWhiteSpace(emailNormalized) && _db.Users.Any(u => u.Email.ToLower() == emailNormalized))
            {
                ViewBag.Error = "Email đã được sử dụng.";
                return View();
            }

            // --- 2️⃣ Tạo User ---
            var passwordHash = PasswordHasherWithFixedSalt.HashPassword(password);
            var newUser = new User
            {
                Username = usernameNormalized,
                Email = emailNormalized,
                Role = "Customer",
                Password = passwordHash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            };

            try
            {
                _db.Users.Add(newUser);
                _db.SaveChanges();
                Console.WriteLine($"[USER] Thêm thành công User: {newUser.Username} (Id={newUser.IdUser})");
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Lỗi khi thêm User: {ex.InnerException?.Message ?? ex.Message}";
                return View();
            }

            // --- 3️⃣ Tạo Customer (liên kết với User vừa tạo) ---
            try
            {
                var newCustomer = new Customer
                {
                    IdUser = newUser.IdUser,
                    FullName = fullname,
                    Address = address,
                    Phone = phone,
                    User = null // ❗Rất quan trọng để EF không tạo lại User
                };

                _db.Customers.Add(newCustomer);
                _db.SaveChanges();
                try
                {
                    var (lat, lon) = await _geo.GetCoordinatesAsync(newCustomer.Address);
                    if (lat != 0 && lon != 0)
                    {
                        newCustomer.Latitude = lat;
                        newCustomer.Longitude = lon;
                        _db.SaveChanges();
                        Console.WriteLine($"🌍 Đã cập nhật toạ độ cho {newCustomer.FullName}: {lat}, {lon}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Không tìm thấy toạ độ cho địa chỉ: {newCustomer.Address}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GEO-ERROR] Lỗi khi lấy toạ độ: {ex.Message}");
                }
                Console.WriteLine($"[CUSTOMER] Thêm thành công Customer với IdUser={newUser.IdUser}");
                await _logService.LogAsync(newUser.IdUser, $"Người dùng '{newUser.Username}' đã đăng ký tài khoản thành công với vai trò ({newUser.Role}).");
            }
            catch (Exception ex)
            {
                // Nếu thêm Customer lỗi → rollback thủ công
                try
                {
                    var userToRemove = _db.Users.Find(newUser.IdUser);
                    if (userToRemove != null)
                    {
                        _db.Users.Remove(userToRemove);
                        _db.SaveChanges();
                        Console.WriteLine($"[ROLLBACK] Đã xóa User Id={newUser.IdUser} do lỗi Customer");
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"[ROLLBACK-ERROR] {ex2.InnerException?.Message ?? ex2.Message}");
                }

                ViewBag.Error = $"Lỗi khi thêm Customer: {ex.InnerException?.Message ?? ex.Message}";
                return View();
            }

            // --- 4️⃣ Thành công ---
            TempData["Success"] = "Đăng ký thành công! Hãy đăng nhập.";
            return RedirectToAction("Login");
        }

        // 🔹 Đăng nhập bằng Google (OAuth)
        [HttpGet]
        public IActionResult LoginGoogle(string returnUrl = "/")
        {
            // redirect về action riêng sau khi Google xác thực xong
            var redirectUrl = Url.Action("GoogleResponse", "Account", new { returnUrl });
            var props = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        // 🔹 Nhận thông tin từ Google callback
        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string? returnUrl = "/")
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal?.Identities.FirstOrDefault()?.Claims;

            if (claims == null)
                return RedirectToAction("Login");

            // 🔹 Các claim phổ biến từ Google
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var fullName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? email;
            var givenName = claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
            var familyName = claims.FirstOrDefault(c => c.Type == "family_name")?.Value;
            var pictureUrl = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:google:picture")?.Value;
            /////////////////đánh dấu
            var googleId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            // ✅ Kiểm tra hoặc tạo mới User
            var user = _db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Username = fullName,
                    Email = email,
                    Role = "Customer",
                    Password = string.Empty, // đăng nhập Google không cần mật khẩu
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                };
                _db.Users.Add(user);
                _db.SaveChanges();
            }

            // ✅ Kiểm tra hoặc tạo mới Customer
            var customer = _db.Customers.FirstOrDefault(c => c.IdUser == user.IdUser);
            if (customer == null)
            {
                customer = new Customer
                {
                    IdUser = user.IdUser,
                    FullName = fullName ?? $"{givenName} {familyName}".Trim(),
                    Address = null,       // Google không cung cấp
                    Latitude = null,      // Google không cung cấp
                    Longitude = null,     // Google không cung cấp
                    Phone = null,         // Google không cung cấp
                    CreatedAt = DateTime.Now
                };
                _db.Customers.Add(customer);
                _db.SaveChanges();
            }

            // ✅ Tạo cookie đăng nhập
            var userClaims = new List<Claim>
    {
                new(ClaimTypes.Name, user.Username ?? fullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role),
                new(ClaimTypes.NameIdentifier, googleId),
                new("AppUserId", user.IdUser.ToString()),
                new("FullName", fullName)
            };

            if (!string.IsNullOrEmpty(pictureUrl))
                userClaims.Add(new Claim("picture", pictureUrl));

            var identity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Redirect(returnUrl ?? "/");
        }

        // 🔹 Đăng xuất
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue("AppUserId");
            if (int.TryParse(userId, out var id))
            {
                var user = _db.Users.FirstOrDefault(u => u.IdUser == id);
                if (user != null)
                {
                    user.IsOnline = false;
                    _db.SaveChanges();
                }
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();
    }
}
