using DirtyCoins.Data;
using DirtyCoins.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirtyCoins.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcedureController : ControllerBase
    {
        private readonly ProcedureService _procedureService;
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;
        private readonly ILogger<ProcedureController> _logger;

        public ProcedureController(
            ProcedureService procedureService,
            ApplicationDbContext context,
            SystemLogService logService,
            ILogger<ProcedureController> logger)
        {
            _procedureService = procedureService;
            _context = context;
            _logService = logService;
            _logger = logger;
        }

        // 🔹 0. Chạy toàn bộ procedure
        [HttpPost("RunAll")]
        public async Task<IActionResult> RunAll()
        {
            try
            {
                var now = DateTime.Now;
                int month = now.Month;
                int year = now.Year;

                _logger.LogInformation("===== BẮT ĐẦU CHẠY TOÀN BỘ PROCEDURE ({Time}) =====", now);
                await _logService.LogAsync(GetCurrentUserId(), "RunAllProcedures");

                // 1️⃣ AddImportedToInventory
                var importIds = await _context.Importeds.Select(i => i.IdImported).ToListAsync();
                foreach (var id in importIds)
                    await _procedureService.AddImportedToInventory(id);

                // 2️⃣ sp_UpdateStoreReport
                var storeIds = await _context.Stores.Select(s => s.IdStore).ToListAsync();
                foreach (var sid in storeIds)
                    await _procedureService.UpdateStoreReport(sid);

                // 3️⃣ UpdateMonthlyInventoryAndStock
                await _procedureService.UpdateMonthlyInventoryAndStock(month, year);

                // 4️⃣ UpdateCustomerRankStats
                await _procedureService.UpdateCustomerRankStats(month, year);

                _logger.LogInformation("===== HOÀN THÀNH CHẠY TOÀN BỘ PROCEDURE ({Time}) =====", DateTime.Now);

                return Ok(new { success = true, message = "✅ Đã chạy toàn bộ stored procedure thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi chạy toàn bộ procedure!");
                await _logService.LogAsync(GetCurrentUserId(), "RunAllProcedures_Error: " + ex.Message);
                return BadRequest(new { success = false, message = "❌ " + ex.Message });
            }
        }

        // 🔹 1. Chạy AddImportedToInventory
        [HttpPost("AddImportedToInventory/{id}")]
        public async Task<IActionResult> AddImportedToInventory(int id)
        {
            try
            {
                await _procedureService.AddImportedToInventory(id);
                await _logService.LogAsync(GetCurrentUserId(), $"AddImportedToInventory(Id={id})");
                return Ok(new { success = true, message = "✅ Đã cập nhật kho từ phiếu nhập #" + id });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(GetCurrentUserId(), $"❌ Lỗi AddImportedToInventory(Id={id}): {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 2. Cập nhật báo cáo cửa hàng
        [HttpPost("UpdateStoreReport/{idStore}")]
        public async Task<IActionResult> UpdateStoreReport(int idStore)
        {
            try
            {
                await _procedureService.UpdateStoreReport(idStore);
                await _logService.LogAsync(GetCurrentUserId(), $"UpdateStoreReport(IdStore={idStore})");
                return Ok(new { success = true, message = "✅ Đã cập nhật báo cáo cửa hàng #" + idStore });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(GetCurrentUserId(), $"❌ Lỗi UpdateStoreReport(IdStore={idStore}): {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 3. Tổng hợp tồn kho
        [HttpPost("UpdateMonthlyInventory")]
        public async Task<IActionResult> UpdateMonthlyInventory()
        {
            try
            {
                var now = DateTime.Now;
                await _procedureService.UpdateMonthlyInventoryAndStock(now.Month, now.Year);
                await _logService.LogAsync(GetCurrentUserId(), "UpdateMonthlyInventoryAndStock");
                return Ok(new { success = true, message = "✅ Đã tổng hợp tồn kho tháng hiện tại" });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(GetCurrentUserId(), $"❌ Lỗi UpdateMonthlyInventory: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 4. Cập nhật hạng khách hàng
        [HttpPost("UpdateCustomerRank")]
        public async Task<IActionResult> UpdateCustomerRank()
        {
            try
            {
                var now = DateTime.Now;
                await _procedureService.UpdateCustomerRankStats(now.Month, now.Year);
                await _logService.LogAsync(GetCurrentUserId(), "UpdateCustomerRankStats");
                return Ok(new { success = true, message = "✅ Đã cập nhật hạng khách hàng tháng hiện tại" });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(GetCurrentUserId(), $"❌ Lỗi UpdateCustomerRankStats: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔹 5. Test tạo đơn hàng
        [HttpPost("CreateOrderTransactional")]
        public async Task<IActionResult> CreateOrderTransactional()
        {
            try
            {
                await _procedureService.CreateOrder_Transactional();
                await _logService.LogAsync(GetCurrentUserId(), "CreateOrder_Transactional");
                return Ok(new { success = true, message = "✅ Đã chạy CreateOrder_Transactional (test)" });
            }
            catch (Exception ex)
            {
                await _logService.LogAsync(GetCurrentUserId(), $"❌ Lỗi CreateOrder_Transactional: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 🔸 Hàm lấy ID user hiện tại từ Claims
        private int? GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
