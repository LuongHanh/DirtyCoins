using DirtyCoins.Data;
using DirtyCoins.Hubs;
using DirtyCoins.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ================================
// 1️⃣ Load cấu hình
// ================================
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // 🔹 Cho phép đọc biến môi trường từ Render hoặc hệ thống

// Load file .env (chạy local)
Env.Load();

// ================================
// 3️⃣ Kết nối CSDL
// ================================
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Database=DirtyCoinsDB;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)
    )
);

// ================================
// 4️⃣ Logging với Serilog
// ================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("wwwroot/logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ================================
// 5️⃣ Đăng ký các services
// ================================
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProcedureService>();
builder.Services.AddHostedService<ScheduledJobService>();
builder.Services.AddScoped<GeocodingService>();
builder.Services.AddScoped<SystemLogService>();
builder.Services.AddHostedService<MaintenanceScheduler>();

// ================================
// 6️⃣ Cấu hình MVC + Session + SignalR
// ================================
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSignalR();

// ================================
// 7️⃣ Cấu hình Google Login
// ================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options => { options.LoginPath = "/Account/Login"; })
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
                       ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
                           ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";
    options.CallbackPath = "/signin-google";
    if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        Console.WriteLine("⚠️ Google Auth chưa cấu hình!");

    options.SaveTokens = true;
});

// ================================
// 8️⃣ Build app
// ================================
var app = builder.Build();

// ================================
// 9️⃣ Middleware pipeline
// ================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<MaintenanceMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// ================================
// 🔹 SignalR hubs
// ================================
app.MapHub<CategoryHub>("/categoryHub");
app.MapHub<SystemHub>("/systemHub");

// ================================
// 🔹 MVC routes
// ================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// ================================
// 🔹 Start app
// ================================
app.Run();
