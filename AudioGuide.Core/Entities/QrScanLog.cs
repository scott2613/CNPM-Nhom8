namespace AudioGuide.Core.Entities;

// Thực thể ghi nhận lịch sử từng lượt quét mã QR theo thời gian thực
public class QrScanLog
{
 public Guid? EventId {get;set;}
 public string EventType {get;set;}="Legacy";
 public string? VisitorId {get;set;}
 public int? PoiId {get;set;}
 public string LanguageCode {get;set;}="vi";
 public bool IsUtc {get;set;}

    // Khóa chính tự tăng của lượt quét
    public int Id { get; set; }

    // Mã QR được quét tại thời điểm tương tác
    public string QrToken { get; set; } = string.Empty;

    // Mã POI nhận diện được tại thời điểm quét
    public string? PoiCode { get; set; }

    // Thời điểm quét tính theo giờ chuẩn quốc tế UTC
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    // Địa chỉ IP của thiết bị thực hiện thao tác quét
    public string? IpAddress { get; set; }

    // Chuỗi thông tin hệ điều hành và trình duyệt của du khách
    public string? UserAgent { get; set; }
}