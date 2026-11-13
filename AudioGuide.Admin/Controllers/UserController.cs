// Import các thành phần Controller của ASP.NET Core
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Admin.Controllers;

// Controller điều khiển trang giám sát lượt du khách và số liệu tương tác thời gian thực
public class UserController : Controller
{
    // Factory tạo kết nối HttpClient
    private readonly IHttpClientFactory _httpClientFactory;

    // Constructor chuẩn tiêm phụ thuộc IHttpClientFactory
    public UserController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // 1. Hiển thị trang thống kê du khách thời gian thực
    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            // Lấy thông tin thống kê Realtime từ API
            var response = await client.GetFromJsonAsync<RealtimeMetricsApiResponse>("api/analytics/realtime-metrics", HttpContext.RequestAborted);
            ViewBag.TotalScansToday = response?.TotalScansToday ?? 0;
            ViewBag.ActiveUsersNow = response?.ActiveUsersNow ?? 0;
            ViewBag.TopPoi = response?.TopPoi ?? "Không có người nghe";
            ViewBag.PeakHour = response?.PeakHour ?? "N/A";

            return View(response?.Logs ?? new List<UserAccessLogViewModel>());
        }
        catch
        {
            ViewBag.TotalScansToday = 0;
            ViewBag.ActiveUsersNow = 0;
            ViewBag.TopPoi = "Chưa xác định";
            ViewBag.PeakHour = "N/A";
            ViewData["ApiError"] = "Không tải được dữ liệu từ API. Hãy thử lại.";
            return View(new List<UserAccessLogViewModel>());
        }
    }

    // 2. Endpoint phục vụ AJAX Polling cập nhật số liệu Dashboard định kỳ mỗi 3 giây
    [HttpGet]
    public async Task<IActionResult> GetRealtimeMetrics()
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        try
        {
            var response = await client.GetFromJsonAsync<RealtimeMetricsApiResponse>("api/analytics/realtime-metrics", HttpContext.RequestAborted);
            return Json(response);
        }
        catch
        {
            return StatusCode(503, new {message="Không tải được số liệu từ API."});
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> GetGateEntriesByHour([FromQuery] string range = "1d") =>
        ForwardReport("gate-entries-by-hour", range);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> GetPoiInteractions([FromQuery] string range = "1d") =>
        ForwardReport("poi-interactions", range);

    private async Task<IActionResult> ForwardReport(string report, string range)
    {
        var client = _httpClientFactory.CreateClient("AudioGuideApiClient");
        var cancellationToken = HttpContext.RequestAborted;
        try
        {
            using var response = await client.GetAsync(
                $"api/analytics/{report}?range={Uri.EscapeDataString(range)}", cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "API thống kê phản hồi quá chậm. Hãy thử lại." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "Không kết nối được API thống kê. Hãy thử lại." });
        }
    }
}

// Lớp ánh xạ dữ liệu phản hồi chỉ số thời gian thực từ API
public class RealtimeMetricsApiResponse
{
    public int TotalScansToday { get; set; }
    public int ActiveUsersNow { get; set; }
    public string TopPoi { get; set; } = string.Empty;
    public string PeakHour { get; set; } = string.Empty;
    public List<UserAccessLogViewModel> Logs { get; set; } = new();
}

// ViewModel chi tiết từng lượt tương tác của du khách
public class UserAccessLogViewModel
{
    // Khai báo kiểu string để tương thích chuỗi SessionId dạng "SESS-xxx"
    public string Id { get; set; } = string.Empty;
    public string PoiCode { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string ScannedAt { get; set; } = string.Empty;
}
