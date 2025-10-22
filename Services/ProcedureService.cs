using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DirtyCoins.Services
{
    public class ProcedureService
    {
        private readonly string _conn;

        public ProcedureService(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection");
        }

        // 🔹 Hàm chạy procedure chung
        private async Task ExecuteAsync(string proc, params SqlParameter[] parameters)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                using var cmd = new SqlCommand(proc, conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi chạy procedure [{proc}]: {ex.Message}", ex);
            }
        }

        // 🔹 0. Tạo đơn hàng (gọi khi khách đặt hàng hoặc test)
        public Task CreateOrder_Transactional(int idCustomer, int idStore, string itemsJson)
            => ExecuteAsync("CreateOrder_Transactional",
                new SqlParameter("@IdCustomer", idCustomer),
                new SqlParameter("@IdStore", idStore),
                new SqlParameter("@Items", itemsJson));

        // ✅ Overload: cho phép test chạy procedure mà không cần tham số
        public Task CreateOrder_Transactional()
            => ExecuteAsync("CreateOrder_Transactional");

        // 🔹 1. Cập nhật kho khi có phiếu nhập
        public Task AddImportedToInventory(int idImported)
            => ExecuteAsync("AddImportedToInventory",
                new SqlParameter("@IdImported", idImported));

        // 🔹 2. Cập nhật báo cáo doanh thu cửa hàng
        public Task UpdateStoreReport(int idStore)
            => ExecuteAsync("sp_UpdateStoreReport",
                new SqlParameter("@IdStore", idStore));

        // 🔹 3. Tổng hợp tồn kho hàng tháng
        public Task UpdateMonthlyInventoryAndStock(int month, int year)
            => ExecuteAsync("UpdateMonthlyInventoryAndStock",
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year));

        // 🔹 4. Cập nhật hạng khách hàng
        public Task UpdateCustomerRankStats(int month, int year)
            => ExecuteAsync("UpdateCustomerRankStats",
                new SqlParameter("@Month", month),
                new SqlParameter("@Year", year));
    }
}
