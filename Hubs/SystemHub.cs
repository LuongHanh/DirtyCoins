using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DirtyCoins.Hubs
{
    public class SystemHub : Hub
    {
        // Gửi thông báo đến tất cả client
        public async Task NotifyMaintenance(bool isImportant, string message, string startTime, string endTime)
        {
            await Clients.All.SendAsync("MaintenanceAlert", new
            {
                IsImportant = isImportant,
                Message = message,
                StartTime = startTime,
                EndTime = endTime
            });
        }
    }
}
