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
            _logger.LogInformation("🕓 MaintenanceScheduler đang khởi động...");

            try
            {
                // Đợi 5 giây để đảm bảo DB và SignalR sẵn sàng (rất quan trọng khi chạy Render/Azure)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("⚠️ MaintenanceScheduler bị hủy khi khởi động (delay startup).");
                return; // kết thúc nhẹ nhàng, không crash host
            }

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

                        await _hubContext.Clients.All.SendAsync("MaintenanceAlert", new
                        {
                            pending.IsImportant,
                            pending.StartTime,
                            pending.EndTime,
                            pending.Reason
                        });

                        if (pending.IsImportant)
                        {
                            await _hubContext.Clients.All.SendAsync("ForceMaintenance", new
                            {
                                Message = "🚨 Hệ thống sẽ tạm dừng trong giây lát để bảo trì quan trọng.",
                                RedirectAfter = 5
                            });
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("MaintenanceScheduler bị hủy do app dừng (TaskCanceledException).");
                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("MaintenanceScheduler bị hủy do token (OperationCanceledException).");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Lỗi MaintenanceScheduler: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("⏹ MaintenanceScheduler bị hủy khi chờ (delay loop).");
                    break;
                }
            }

            _logger.LogInformation("🧹 MaintenanceScheduler đã dừng.");
        }
    }
}
