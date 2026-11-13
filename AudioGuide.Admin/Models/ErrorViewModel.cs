namespace AudioGuide.Admin.Models;

// ViewModel chứa dữ liệu theo dõi mã lỗi yêu cầu phục vụ hiển thị trên trang Error.cshtml
public class ErrorViewModel
{
    // Mã định danh truy vết yêu cầu (RequestId hoặc HttpContext.TraceIdentifier)
    public string? RequestId { get; set; }

    // Cờ kiểm tra có hiển thị mã truy vết lỗi trên giao diện hay không
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}