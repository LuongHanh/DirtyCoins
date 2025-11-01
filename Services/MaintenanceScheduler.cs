using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace DirtyCoins.Services
{
    public class MaintenanceScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MaintenanceScheduler> _logger;
        private readonly IHubContext<SystemHub> _hubContext;
        private readonly string _filePath;

        public MaintenanceScheduler(
            IServiceProvider serviceProvider,
            IWebHostEnvironment env,
            ILogger<MaintenanceScheduler> logger,
            IHubContext<SystemHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _env = env;
            _logger = logger;
            _hubContext = hubContext;
            _filePath = Path.Combine(_env.ContentRootPath, "maintenance.json");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🕓 MaintenanceScheduler đang chạy...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var pending = await db.MaintenanceLogs
                        .Where(m => !m.IsActive && m.StartTime <= DateTime.Now && m.EndTime > DateTime.Now)
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstOrDefaultAsync(stoppingToken);

                    if (pending != null)
                    {
                        pending.IsActive = true;
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation($"✅ Đã kích hoạt bảo trì: {pending.Reason}");

                        var json = JsonSerializer.Serialize(new
                        {
                            IsMaintenance = true,
                            pending.StartTime,
                            pending.EndTime,
                            pending.Reason,
                            pending.IsImportant
                        });
                        await File.WriteAllTextAsync(_filePath, json, stoppingToken);

                        // 🔔 Gửi thông báo realtime đến toàn bộ client
                        await _hubContext.Clients.All.SendAsync("MaintenanceAlert", new
                        {
                            pending.IsImportant,
                            pending.StartTime,
                            pending.EndTime,
                            pending.Reason
                        });

                        // Nếu là bảo trì quan trọng → cho client biết sắp chặn
                        if (pending.IsImportant)
                        {
                            await _hubContext.Clients.All.SendAsync("ForceMaintenance", new
                            {
                                Message = "🚨 Hệ thống sẽ tạm dừng trong giây lát để bảo trì quan trọng.",
                                RedirectAfter = 5 // giây
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Lỗi MaintenanceScheduler: {ex.Message}");
                }
                //Nếu chạy local thì comment dòng này để test nhanh hơn
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
