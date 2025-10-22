using DirtyCoins.Data;
using DirtyCoins.Hubs;
using DirtyCoins.Models;
using DirtyCoins.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text.Json;
using static DirtyCoins.Controllers.AdminController;

[ApiController]
[Route("system")]
public class SystemController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    private readonly IHubContext<SystemHub> _hubContext;

    public SystemController(ApplicationDbContext context, IWebHostEnvironment env, IHubContext<SystemHub> hubContext)
    {
        _context = context;
        _env = env;
        _hubContext = hubContext;
    }

    private string FilePath => Path.Combine(_env.ContentRootPath, "maintenance.json");
    private int? GetCurrentUserId()
    {
        string? idStr = User.FindFirstValue("AppUserId")
                        ?? User.FindFirstValue("IdUser");
        return int.TryParse(idStr, out int id) ? id : null;
    }
    // 🟡 Bắt đầu bảo trì
    [HttpPost("stop")]
    public async Task<IActionResult> StopSystem([FromBody] MaintenanceRequest input)
    {
        try
        {
            var userId = GetCurrentUserId();

            var log = new MaintenanceLog
            {
                IdUser = userId > 0 ? userId : null,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                Reason = input.Reason ?? "Bảo trì hệ thống",
                CreatedAt = DateTime.Now,
                IsActive = DateTime.Now == input.StartTime ? true : false,
                IsImportant = input.IsImportant
            };

            _context.MaintenanceLogs.Add(log);
            await _context.SaveChangesAsync();

            // Ghi file JSON
            var json = JsonSerializer.Serialize(new
            {
                IsMaintenance = true,
                input.StartTime,
                input.EndTime,
                input.Reason,
                input.IsImportant
            });
            await System.IO.File.WriteAllTextAsync(FilePath, json);

            // 🔔 Gửi thông báo realtime tới tất cả user
            await _hubContext.Clients.All.SendAsync("MaintenanceAlert", new
            {
                IsImportant = input.IsImportant,
                Message = input.Reason,
                StartTime = input.StartTime,
                EndTime = input.EndTime
            });

            // Nếu bảo trì quan trọng → yêu cầu client redirect
            if (input.IsImportant)
            {
                if(log.IsActive == true)
                {
                    return Json(new
                    {
                        success = true,
                        message = "🚨 Hệ thống sẽ tạm dừng sau 5 giây để bảo trì.",
                        redirectUrl = "/Maintenance"
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        message = $"🚨 Hệ thống bảo trì bắt đầu từ {log.StartTime} đến {log.EndTime}",
                    });
                }
            }

            return Json(new
            {
                success = true,
                message = "⚙️ Đã thiết lập lịch bảo trì (không quan trọng). Người dùng sẽ được thông báo trước.",
            });
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? "Không có inner exception.";
            Console.WriteLine("❌ MaintenanceLog Save Error: " + inner);
            return Json(new { message = "❌ Lỗi khi lưu MaintenanceLog: " + inner });
        }
    }

    // 🟢 Khởi động lại hệ thống
    [HttpPost("restart")]
    public async Task<IActionResult> RestartSystem()
    {
        var current = _context.MaintenanceLogs.FirstOrDefault(m => m.IsActive);
        if (current != null)
        {
            current.IsActive = false;
            await _context.SaveChangesAsync();
        }

        if (System.IO.File.Exists(FilePath))
            System.IO.File.Delete(FilePath);

        return Json(new { message = "✅ Hệ thống đã hoạt động trở lại bình thường." });
    }

    // Trang thông báo bảo trì
    [HttpGet("/Maintenance")]
    public IActionResult Maintenance()
    {
        var path = Path.Combine(_env.ContentRootPath, "maintenance.json");
        if (!System.IO.File.Exists(path))
            return Redirect("/");

        var json = System.IO.File.ReadAllText(path);
        var info = JsonSerializer.Deserialize<MaintenanceInfo>(json);

        ViewBag.StartTime = info?.StartTime.ToString("HH:mm dd/MM/yyyy");
        ViewBag.EndTime = info?.EndTime.ToString("HH:mm dd/MM/yyyy");
        ViewBag.Reason = info?.Reason ?? "Bảo trì hệ thống định kỳ";

        return View("~/Views/Shared/Maintenance.cshtml");
    }

    // GET: /system/restart-confirm
    [HttpGet("restart-confirm")]
    public IActionResult RestartConfirm()
    {
        return View("RestartConfirm");
    }

    [HttpPost("do-restart")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DoRestart([FromForm] string username, [FromForm] string password)
    {
        try
        {
            var normalizeHash = PasswordHasherWithFixedSalt.HashPassword(password);

            var admin = _context.Users.FirstOrDefault(u =>
                u.Username == username && u.Password == normalizeHash && u.Role == "Admin");

            if (admin == null)
            {
                ViewBag.Error = "❌ Sai tài khoản hoặc mật khẩu admin.";
                return View("RestartConfirm");
            }

            // ✅ Tắt chế độ bảo trì trong DB
            var current = _context.MaintenanceLogs.FirstOrDefault(m => m.IsActive);
            if (current != null)
            {
                current.IsActive = false;
                await _context.SaveChangesAsync();
            }

            // ✅ Xóa file maintenance.json
            if (System.IO.File.Exists(FilePath))
                System.IO.File.Delete(FilePath);

            // ✅ Đăng nhập lại admin
            HttpContext.Session.SetInt32("UserId", admin.IdUser);
            HttpContext.Session.SetString("Username", admin.Username);
            HttpContext.Session.SetString("Role", admin.Role);

            TempData["Success"] = "✅ Hệ thống đã được khởi động lại thành công!";
            return RedirectToAction("Dashboard", "Admin");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Restart Error: {ex.Message}");
            ViewBag.Error = "Đã xảy ra lỗi trong quá trình khởi động lại.";
            return View("RestartConfirm");
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var lastLog = _context.MaintenanceLogs
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        // Nếu không có bản ghi hoặc đã tắt bảo trì => hệ thống hoạt động bình thường
        if (lastLog == null || !lastLog.IsActive)
            return Json(new { IsMaintenance = false });

        // Nếu có bản ghi bảo trì và đang bật
        return Json(new
        {
            IsMaintenance = true,
            lastLog.StartTime,
            lastLog.EndTime,
            lastLog.Reason,
            lastLog.IsImportant
        });
    }

    private class MaintenanceInfo
    {
        public bool IsMaintenance { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Reason { get; set; }
    }
}
