namespace AudioGuide.Core.Entities;

// Thực thể quản lý mã QR định danh dán tại hiện vật
public class QrCode
{
    // Khóa chính tự tăng của bản ghi QR
    public int Id { get; set; }

    // Khóa ngoại liên kết với POI (cho phép null nếu tạo mã QR trước khi gán địa điểm)
    public int? PoiId { get; set; }

    // Chuỗi token mã hóa trong hình QR
    public string QrToken { get; set; } = string.Empty;

    // Tổng số lượt du khách đã quét mã này
    public int ScanCount { get; set; } = 0;

    // Trạng thái hiệu lực của mã QR
    public bool IsActive { get; set; } = true;

    // Đối tượng POI gắn với mã QR này
    public Poi? Poi { get; set; }
}