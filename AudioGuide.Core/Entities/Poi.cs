// Thư viện xử lý kiểu dữ liệu hình học và tọa độ GIS
using NetTopologySuite.Geometries;

namespace AudioGuide.Core.Entities;

// Thực thể đại diện cho điểm tham quan di tích / bảo tàng
public class Poi
{
 public byte[] RowVersion {get;set;}=[];
    // Khóa chính tự tăng trong database
    public int Id { get; set; }

    // Mã định danh tĩnh của địa điểm (ví dụ: POI-WRM-TIGERCELL)
    public string Code { get; set; } = string.Empty;

    // Phân tầng hiển thị (0: Ngoài trời/Trệt, 1: Tầng 1, 2: Tầng 2)
    public int Floor { get; set; } = 0;

    // Tọa độ hình học Point chứa kinh độ và vĩ độ chuẩn WGS 84
    public Point Location { get; set; } = default!;

    // Bán kính tự động phát thuyết minh khi du khách tiếp cận (đơn vị: mét)
    public double TriggerRadiusMeters { get; set; } = 5.0;

    // Đường dẫn ảnh đại diện của hiện vật hoặc điểm tham quan
    public string? ImageUrl { get; set; }

    // Trạng thái hiển thị (true: đang mở cho khách, false: tạm đóng/ẩn)
    public bool IsActive { get; set; } = true;

    // Thời điểm tạo bản ghi tính theo giờ UTC
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Danh sách các bản thuyết minh đa ngôn ngữ tương ứng
    public ICollection<PoiTranslation> Translations { get; set; } = new List<PoiTranslation>();

    // Mã QR vật lý liên kết trực tiếp với địa điểm này
    public QrCode? QrCode { get; set; }
}