using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DirtyCoins.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _httpClient;

        public GeocodingService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DirtyCoinsApp/1.0 (contact: dirtycoins@example.com)");
        }

        // 🔹 Hàm chuẩn hóa địa chỉ (xử lý tầng, TTTM, thêm quốc gia)
        private string NormalizeAddress(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            string addr = raw.Trim();

            // Loại bỏ các từ không cần thiết mà API OSM hay lỗi
            addr = Regex.Replace(addr, @"\b(Tầng|TTTM|Tòa nhà|Số|Khu vực)\b", "", RegexOptions.IgnoreCase);
            addr = addr.Replace("  ", " ").Trim();

            // Thêm Việt Nam vào cuối nếu thiếu
            if (!addr.ToLower().Contains("việt nam") && !addr.ToLower().Contains("vietnam"))
                addr += ", Việt Nam";

            return addr;
        }

        // 🔹 Gọi API Nominatim (OSM)
        private async Task<(double, double)> CallOpenStreetMapAsync(string address)
        {
            string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(address)}&format=json&limit=1";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return (0, 0);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.GetArrayLength() == 0)
                    return (0, 0);

                var first = doc.RootElement[0];
                double lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                double lon = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

                return (lat, lon);
            }
            catch
            {
                return (0, 0);
            }
        }

        // 🔹 Gọi API Photon (fallback miễn phí, dữ liệu OSM)
        private async Task<(double, double)> CallPhotonAsync(string address)
        {
            string url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(address)}&limit=1";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return (0, 0);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                    return (0, 0);

                var coords = features[0].GetProperty("geometry").GetProperty("coordinates");
                double lon = coords[0].GetDouble();
                double lat = coords[1].GetDouble();

                return (lat, lon);
            }
            catch
            {
                return (0, 0);
            }
        }

        // 🔹 Hàm chính gọi API định vị (có fallback)
        public async Task<(double lat, double lon)> GetCoordinatesAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return (0, 0);

            string normalized = NormalizeAddress(address);
            Console.WriteLine($"🌍 [LOOKUP] {normalized}");

            // 1️⃣ Gọi OpenStreetMap trước
            var (lat, lon) = await CallOpenStreetMapAsync(normalized);

            // 2️⃣ Nếu fail → gọi Photon
            if (lat == 0 && lon == 0)
            {
                Console.WriteLine($"⚠️ [OSM FAIL] thử Photon API...");
                (lat, lon) = await CallPhotonAsync(normalized);
            }

            if (lat == 0 && lon == 0)
                Console.WriteLine($"❌ [NOT FOUND] {normalized}");
            else
                Console.WriteLine($"✅ [FOUND] {normalized} → ({lat}, {lon})");

            await Task.Delay(1000); // tránh bị rate-limit của OSM
            return (lat, lon);
        }
    }
}
