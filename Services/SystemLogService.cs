using System;
using System.Threading.Tasks;
using DirtyCoins.Data;
using DirtyCoins.Models;
using Microsoft.EntityFrameworkCore;

namespace DirtyCoins.Services
{
    public class SystemLogService
    {
        private readonly ApplicationDbContext _context;

        public SystemLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Ghi log hành động của người dùng / hệ thống
        /// </summary>
        /// <param name="userId">ID người thực hiện (null nếu là hệ thống)</param>
        /// <param name="action">Mô tả hành động</param>
        public async Task LogAsync(int? userId, string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;

            try
            {
                var log = new SystemLog
                {
                    IdUser = userId,
                    Action = action.Trim(),
                    TimeStamp = DateTime.Now
                };

                _context.SystemLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"⚠️ Lỗi database khi ghi log: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ghi log thất bại: {ex.Message}");
            }
        }
    }
}
