namespace AudioGuide.Core.DTOs.Common;

// DTO phản hồi trạng thái hoạt động của hệ thống và các dịch vụ liên kết
public class HealthCheckResponseDto
{
    // Trạng thái tổng quan của API (Healthy, Degraded, Unhealthy)
    public string Status { get; set; } = "Healthy";

    // Thời điểm kiểm tra trạng thái tính theo giờ UTC
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Tên môi trường hiện tại (Development, Staging, Production)
    public string Environment { get; set; } = string.Empty;

    // Trạng thái kết nối cơ sở dữ liệu
    public bool DatabaseConnected { get; set; }

    // Danh sách chi tiết trạng thái của các dịch vụ ngoại vi (TTS, Dịch thuật)
    public Dictionary<string, string> Services { get; set; } = new();
}