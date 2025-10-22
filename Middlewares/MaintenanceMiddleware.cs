using System.Text.Json;
using DirtyCoins.Data;
using DirtyCoins.Models;
using Microsoft.EntityFrameworkCore;

public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly string _filePath;
    private readonly IServiceProvider _serviceProvider; // ✅ dùng scope an toàn

    public MaintenanceMiddleware(RequestDelegate next, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _next = next;
        _env = env;
        _serviceProvider = serviceProvider;
        _filePath = Path.Combine(_env.ContentRootPath, "maintenance.json");
    }

    public async Task Invoke(HttpContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ⚙️ Lấy bản ghi bảo trì mới nhất
        var log = await db.MaintenanceLogs
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        // ❌ Không có bản ghi hoặc đã tắt -> hệ thống hoạt động bình thường
        if (log == null || !log.IsActive)
        {
            await _next(context);
            return;
        }

        var now = DateTime.Now;

        // 🕓 Nếu chưa tới giờ bắt đầu bảo trì → cho đi
        if (now < log.StartTime)
        {
            await _next(context);
            return;
        }

        // 🟡 Nếu là bảo trì nhẹ → chỉ cảnh báo chứ không chặn
        if (!log.IsImportant)
        {
            context.Response.Headers["X-Maintenance-Warning"] =
                $"⚠️ Hệ thống đang bảo trì nhẹ ({log.StartTime:HH:mm} - {log.EndTime:HH:mm}): {log.Reason ?? "Không có mô tả"}";
            await _next(context);
            return;
        }

        // 🚫 Nếu là bảo trì quan trọng → chặn toàn bộ trừ các đường dẫn được phép
        var path = context.Request.Path.ToString().ToLower();

        if (path.StartsWith("/maintenance") ||
            path.StartsWith("/system/restart-confirm") ||
            path.StartsWith("/system/status") ||
            path.StartsWith("/system/do-restart") ||
            path.StartsWith("/admin/override-maintenance") ||
            path.StartsWith("/account/login") ||
            path.StartsWith("/account/logout") ||
            path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/images"))
        {
            await _next(context);
            return;
        }

        // ✅ Cho phép Admin bỏ qua
        var role = context.User?.FindFirst("Role")?.Value ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 🚷 Ngược lại → chuyển hướng đến trang bảo trì
        context.Response.Redirect("/Maintenance");
    }
}
