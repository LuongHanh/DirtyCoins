using DirtyCoins.Data;
using DirtyCoins.Models;
using DirtyCoins.Services;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirtyCoins.Controllers
{
    public class GeoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GeocodingService _geo;

        public GeoController(ApplicationDbContext context, GeocodingService geo)
        {
            _context = context;
            _geo = geo;
        }

        // -------------------------------------------------------
        // 📍 Cập nhật lại toàn bộ toạ độ cho CỬA HÀNG
        // -------------------------------------------------------
        [HttpGet("/geo/update-stores")]
        public async Task<IActionResult> UpdateStoreCoordinates()
        {
            var stores = await _context.Stores
                .Where(s => !string.IsNullOrEmpty(s.Address))
                .ToListAsync();

            int updated = 0;

            foreach (var store in stores)
            {
                // 🔹 Chuẩn hoá địa chỉ (thêm quốc gia nếu thiếu)
                string addressNormalized = store.Address.Trim();
                if (!addressNormalized.ToLower().Contains("việt nam") && !addressNormalized.ToLower().Contains("vietnam"))
                    addressNormalized += ", Việt Nam";

                var (lat, lon) = await _geo.GetCoordinatesAsync(addressNormalized);

                if (lat != 0 && lon != 0)
                {
                    store.Latitude = lat;
                    store.Longitude = lon;

                    updated++;
                    Console.WriteLine($"✅ {store.StoreName}: {lat}, {lon}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Không tìm thấy tọa độ cho: {store.StoreName}");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Đã cập nhật lại toàn bộ toạ độ cửa hàng.",
                updated,
                total = stores.Count
            });
        }

        // -------------------------------------------------------
        // 📍 Cập nhật lại toàn bộ toạ độ cho KHÁCH HÀNG
        // -------------------------------------------------------
        [HttpGet("/geo/update-customers")]
        public async Task<IActionResult> UpdateCustomerCoordinates()
        {
            var customers = await _context.Customers
                .Where(c => !string.IsNullOrEmpty(c.Address))
                .ToListAsync();

            int updated = 0;

            foreach (var customer in customers)
            {
                // 🔹 Chuẩn hoá địa chỉ (thêm quốc gia nếu thiếu)
                string addressNormalized = customer.Address.Trim();
                if (!addressNormalized.ToLower().Contains("việt nam") && !addressNormalized.ToLower().Contains("vietnam"))
                    addressNormalized += ", Việt Nam";

                var (lat, lon) = await _geo.GetCoordinatesAsync(addressNormalized);

                if (lat != 0 && lon != 0)
                {
                    customer.Latitude = lat;
                    customer.Longitude = lon;

                    updated++;
                    Console.WriteLine($"✅ {customer.FullName} ({customer.Address}): {lat}, {lon}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Không tìm thấy tọa độ cho khách: {customer.FullName} ({customer.Address})");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Đã cập nhật lại toàn bộ toạ độ khách hàng.",
                updated,
                total = customers.Count
            });
        }

        // -------------------------------------------------------
        // 📍 Cập nhật tọa độ khi KHÁCH HÀNG ĐỔI ĐỊA CHỈ
        // -------------------------------------------------------
        [HttpPost("/geo/update-customer/{id}")]
        public async Task<IActionResult> UpdateCustomerCoordinateById(int id)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.IdCustomer == id);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Address))
                return NotFound(new { success = false, message = "Không tìm thấy khách hàng hoặc địa chỉ trống." });

            var (lat, lon) = await _geo.GetCoordinatesAsync(customer.Address);

            if (lat == 0 && lon == 0)
                return Ok(new { success = false, message = $"Không thể lấy tọa độ cho địa chỉ: {customer.Address}" });

            customer.Latitude = lat;
            customer.Longitude = lon;

            await _context.SaveChangesAsync();

            Console.WriteLine($"🔄 Cập nhật tọa độ cho {customer.FullName}: {lat}, {lon}");

            return Ok(new
            {
                success = true,
                message = "Đã cập nhật toạ độ khách hàng sau khi thay đổi địa chỉ.",
                lat,
                lon
            });
        }
    }
}
