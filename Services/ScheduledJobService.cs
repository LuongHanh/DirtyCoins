using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DirtyCoins.Services
{
    public class ScheduledJobService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        public ScheduledJobService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // 🔸 1. Nếu là ngày đầu tháng -> cập nhật hạng khách hàng
                if (now.Day == 1)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var proc = scope.ServiceProvider.GetRequiredService<ProcedureService>();
                    await proc.UpdateCustomerRankStats(now.Month - 1 == 0 ? 12 : now.Month - 1,
                                                       now.Month == 1 ? now.Year - 1 : now.Year);
                }

                // 🔸 2. Nếu là ngày cuối tháng -> tổng hợp kho
                var nextDay = now.AddDays(1);
                if (nextDay.Month != now.Month)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var proc = scope.ServiceProvider.GetRequiredService<ProcedureService>();
                    await proc.UpdateMonthlyInventoryAndStock(now.Month, now.Year);
                }

                // Chờ 24h rồi kiểm tra lại
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
