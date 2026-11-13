// Import các thành phần Controller của ASP.NET Core
using Microsoft.AspNetCore.Mvc;

namespace AudioGuide.Admin.Controllers;

// Controller điều phối giao diện quản trị chính và chuyển tiếp dữ liệu POI
public class HomeController : Controller
{
    // Factory quản lý vòng đời của HttpClient
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeController> _logger;

    // Constructor chuẩn tiêm phụ thuộc IHttpClientFactory
    public HomeController(IHttpClientFactory httpClientFactory, ILogger<HomeController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Hàm phụ trợ tạo HttpClient đã được cấu hình sẵn BaseAddress sang AudioGuide.API
    private HttpClient CreateApiClient() => _httpClientFactory.CreateClient("AudioGuideApiClient");

    // 1. Hiển thị màn hình Dashboard danh sách điểm tham quan POI
    public async Task<IActionResult> Index()
    {
        try
        {
            var client = CreateApiClient();
            // Gọi Backend API lấy danh sách POI kèm token hủy request
            var pois = await client.GetFromJsonAsync<List<PoiAdminViewModel>>("api/analytics/pois-admin", HttpContext.RequestAborted);
            // Trả về view kèm danh sách dữ liệu (nếu null thì truyền danh sách rỗng)
            return View(pois ?? new List<PoiAdminViewModel>());
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Không đọc được danh sách POI từ API.");
            // Fallback an toàn về danh sách rỗng để không làm sập giao diện web
            ViewData["ApiError"] = "Không tải được dữ liệu từ API. Hãy thử lại.";
            return View(new List<PoiAdminViewModel>());
        }
    }

    // 2. API Proxy: Lấy lịch sử truy cập phân theo khoảng thời gian
    [HttpGet]
    public async Task<IActionResult> GetUserAccessHistory([FromQuery] string range = "24h")
    {
        string safeRange = Uri.EscapeDataString(range ?? "24h");
        return await ForwardGetAsync($"api/analytics/user-access-history?range={safeRange}");
    }

    // 3. API Proxy: Quét thử (+1 lượt tương tác trực tiếp)
    [HttpPost]
    public IActionResult TestScan(string code) => Ok(new {success=true,message="Chế độ nghe thử; không ghi thống kê."});

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.Mvc.ResponseCache(Duration=0,Location=Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None,NoStore=true)]
    public IActionResult Error() => View(new AudioGuide.Admin.Models.ErrorViewModel {RequestId=HttpContext.TraceIdentifier});

    // 4. API Proxy: Lưu hoặc cập nhật POI kèm tự động dịch đa ngôn ngữ
    [HttpPost]
    public async Task<IActionResult> SavePoi([FromBody] PoiSaveRequest request)
        => await ForwardPostAsJsonAsync("api/analytics/save-poi", request);

    // 5. API Proxy: Đảo trạng thái mở cửa / bảo trì của POI
    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
        => await ForwardPostAsync($"api/analytics/toggle-status/{id}");

    // 6. API Proxy: Xóa vĩnh viễn POI khỏi CSDL
    [HttpDelete]
    public async Task<IActionResult> DeletePoi(int id)
        => await ForwardDeleteAsync($"api/analytics/delete-poi/{id}");

    // 7. API Proxy: Đồng bộ toàn bộ dữ liệu mẫu vào SQL Server
    [HttpPost]
    public async Task<IActionResult> SyncSystem()
        => await ForwardPostAsync("api/analytics/sync-system");

    // 8. API Proxy: Kiểm tra sức khỏe kết nối CSDL và API
    [HttpGet]
    public async Task<IActionResult> CheckHealth()
        => await ForwardGetAsync("api/systemdiagnostics/health-check");

    // Phương thức phụ trợ chuyển tiếp yêu cầu HTTP GET
    private async Task<IActionResult> ForwardGetAsync(string endpoint)
    {
        try
        {
            var client = CreateApiClient();
            using var res = await client.GetAsync(endpoint, HttpContext.RequestAborted);
            return await ProcessHttpResponseAsync(res);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new { success = false, message = "API phản hồi quá chậm. Hãy thử lại." });
        }
        catch (HttpRequestException error)
        {
            _logger.LogWarning(error, "Không kết nối được API tại {Endpoint}.", endpoint);
            return StatusCode(502, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // Phương thức phụ trợ chuyển tiếp yêu cầu HTTP POST không body
    private async Task<IActionResult> ForwardPostAsync(string endpoint)
    {
        try
        {
            var client = CreateApiClient();
            using var res = await client.PostAsync(endpoint, null, HttpContext.RequestAborted);
            return await ProcessHttpResponseAsync(res);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new { success = false, message = "API phản hồi quá chậm. Hãy thử lại." });
        }
        catch (HttpRequestException error)
        {
            _logger.LogWarning(error, "Không kết nối được API tại {Endpoint}.", endpoint);
            return StatusCode(502, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // Phương thức phụ trợ chuyển tiếp yêu cầu HTTP POST kèm dữ liệu JSON
    private async Task<IActionResult> ForwardPostAsJsonAsync<T>(string endpoint, T body)
    {
        try
        {
            var client = CreateApiClient();
            using var res = await client.PostAsJsonAsync(endpoint, body, HttpContext.RequestAborted);
            return await ProcessHttpResponseAsync(res);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new { success = false, message = "API phản hồi quá chậm. Hãy thử lại." });
        }
        catch (HttpRequestException error)
        {
            _logger.LogWarning(error, "Không kết nối được API tại {Endpoint}.", endpoint);
            return StatusCode(502, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // Phương thức phụ trợ chuyển tiếp yêu cầu HTTP DELETE
    private async Task<IActionResult> ForwardDeleteAsync(string endpoint)
    {
        try
        {
            var client = CreateApiClient();
            using var res = await client.DeleteAsync(endpoint, HttpContext.RequestAborted);
            return await ProcessHttpResponseAsync(res);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new { success = false, message = "API phản hồi quá chậm. Hãy thử lại." });
        }
        catch (HttpRequestException error)
        {
            _logger.LogWarning(error, "Không kết nối được API tại {Endpoint}.", endpoint);
            return StatusCode(502, new { success = false, message = "Không kết nối được API. Hãy thử lại." });
        }
    }

    // Xử lý và chuẩn hóa kết quả phản hồi từ Backend API
    private async Task<IActionResult> ProcessHttpResponseAsync(HttpResponseMessage res)
    {
        var content=await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        return new ContentResult {StatusCode=(int)res.StatusCode,Content=content,ContentType=res.Content.Headers.ContentType?.ToString()??"application/json"};
    }
}

// ViewModel ánh xạ danh sách POI hiển thị trên bảng quản trị
public class PoiAdminViewModel
{
 public string RowVersion {get;set;}="";
 public double TriggerRadiusMeters {get;set;}
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string TitleVi { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string DescVi { get; set; } = string.Empty;
    public int Floor { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int ScanCount { get; set; }
}

// DTO gửi yêu cầu lưu hoặc cập nhật địa điểm POI
public class PoiSaveRequest
{
 public string? RowVersion {get;set;}
 public double TriggerRadiusMeters {get;set;}=5;
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;
    public string? DescVi { get; set; }
    public int Floor { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int ScanCount { get; set; }
}