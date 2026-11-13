namespace AudioGuide.Core.Entities;

// Thực thể theo dõi phiên hoạt động của thiết bị khách tham quan
public class DeviceSession
{
    // Khóa chính tự tăng của phiên thiết bị
    public int Id { get; set; }

    // Chuỗi mã băm nhận diện phần cứng/trình duyệt của thiết bị
    public string DeviceFingerprint { get; set; } = string.Empty;

    // Địa chỉ IP gần nhất của thiết bị
    public string IpAddress { get; set; } = "127.0.0.1";

    // Tên thiết bị nhận diện (ví dụ: iPhone 15, Galaxy S24, Windows PC)
    public string DeviceName { get; set; } = "Thiết bị không xác định";

    // Nền tảng hệ điều hành (iOS, Android, Windows, macOS)
    public string Platform { get; set; } = "Web";

    // Trình duyệt đang chạy (Mobile Safari, Chrome, Edge)
    public string Browser { get; set; } = "Browser";

    // Ngôn ngữ du khách đang chọn nghe thuyết minh
    public string PreferredLanguage { get; set; } = "vi";

    // Tổng số lần thiết bị đã tương tác với hệ thống
    public int TotalScans { get; set; } = 0;

    // Cờ chặn thiết bị khi có dấu hiệu gửi request phá hoại hoặc spam
    public bool IsBlocked { get; set; } = false;

    // Thời điểm tương tác gần nhất của thiết bị
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Thời điểm thiết bị truy cập hệ thống lần đầu tiên
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}