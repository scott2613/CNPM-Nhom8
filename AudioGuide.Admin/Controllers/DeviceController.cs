// Import các thành phần Controller của ASP.NET Core
using Microsoft.AspNetCore.Mvc;
// Import thư viện xử lý và phân tích JSON
using System.Text.Json;

namespace AudioGuide.Admin.Controllers;

// Controller quản lý danh sách thiết bị truy cập và kiểm soát quyền chặn/mở
public class DeviceController : Controller
{
    // Factory tạo HttpClient
    private readonly IHttpClientFactory _httpClientFactory;
    // Logger ghi nhận nhật ký lỗi
    private readonly ILogger<DeviceController> _logger;

    // Cấu hình Deserialize không phân biệt hoa thường
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Constructor chuẩn tiêm phụ thuộc qua Dependency Injection
    public DeviceController(IHttpClientFactory httpClientFactory, ILogger<DeviceController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // 1. Hiển thị trang quản lý danh sách thiết bị
    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var response = await client.GetAsync("api/devices", HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                List<DeviceViewModel>? list = null;
                // Phân tích định dạng mảng trực tiếp
                if (json.TrimStart().StartsWith('['))
                {
                    list = JsonSerializer.Deserialize<List<DeviceViewModel>>(json, JsonOptions);
                }
                // Phân tích định dạng đối tượng bọc có thuộc tính devices
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("devices", out var devicesProp))
                    {
                        list = JsonSerializer.Deserialize<List<DeviceViewModel>>(devicesProp.GetRawText(), JsonOptions);
                    }
                }

                return View(list ?? throw new JsonException("Phản hồi thiếu danh sách thiết bị."));
            }

            _logger.LogWarning("API Devices trả về mã lỗi: {StatusCode}", response.StatusCode);
            ViewData["ApiError"] = "Không tải được dữ liệu từ API. Hãy thử lại.";
            return View(new List<DeviceViewModel>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nạp danh sách thiết bị từ Backend API");
            ViewData["ApiError"] = "Không tải được dữ liệu từ API. Hãy thử lại.";
            return View(new List<DeviceViewModel>());
        }
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Presence()
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var response = await client.GetAsync("api/devices/presence", HttpContext.RequestAborted);
            var json = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            return new ContentResult { StatusCode = (int)response.StatusCode, Content = json, ContentType = "application/json" };
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Không đọc được trạng thái phiên thiết bị.");
            return StatusCode(503);
        }
    }

    // 2. Lấy lịch sử truy cập của các thiết bị
    [HttpGet]
    public async Task<IActionResult> GetUserAccessHistory(string range = "24h")
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var res = await client.GetAsync($"api/analytics/user-access-history?range={Uri.EscapeDataString(range)}", HttpContext.RequestAborted);
            var content = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            return new ContentResult { StatusCode = (int)res.StatusCode, Content = content, ContentType = "application/json" };
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Không đọc được lịch sử truy cập.");
            return StatusCode(503, new { message = "Không tải được lịch sử truy cập. Hãy thử lại." });
        }
    }

    // 3. Bật hoặc tắt trạng thái chặn thiết bị
    [HttpPost]
    public async Task<IActionResult> ToggleBlock(int id)
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var res = await client.PostAsync($"api/devices/toggle-block/{id}", null, HttpContext.RequestAborted);
            var content = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            return new ContentResult {StatusCode=(int)res.StatusCode,Content=content,ContentType=res.Content.Headers.ContentType?.ToString()??"application/json"};
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // 4. Xóa vĩnh viễn thiết bị khỏi hệ thống
    [HttpDelete]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var res = await client.DeleteAsync($"api/devices/{id}", HttpContext.RequestAborted);
            var content = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            return new ContentResult {StatusCode=(int)res.StatusCode,Content=content,ContentType=res.Content.Headers.ContentType?.ToString()??"application/json"};
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // 5. Đồng bộ danh sách thiết bị từ bảng nhật ký quét
    [HttpPost]
    public async Task<IActionResult> SyncDevices()
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            using var res = await client.PostAsync("api/devices/sync-devices", null, HttpContext.RequestAborted);
            var content = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            return new ContentResult {StatusCode=(int)res.StatusCode,Content=content,ContentType=res.Content.Headers.ContentType?.ToString()??"application/json"};
        }
        catch (Exception)
        {
            return StatusCode(500, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }
}

// ViewModel ánh xạ thông tin phiên thiết bị của khách tham quan
public class DeviceViewModel
{
 public bool IsOnline {get;set;}
    public int Id { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public int TotalScans { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
