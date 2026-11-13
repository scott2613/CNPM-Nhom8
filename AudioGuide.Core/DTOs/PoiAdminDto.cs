namespace AudioGuide.Core.DTOs;

// DTO chứa danh sách POI phẳng để hiển thị lên bảng điều khiển Admin
public class PoiAdminDto
{
 public string RowVersion {get;set;}="";
 public double TriggerRadiusMeters {get;set;}
    // Mã ID địa điểm
    public int Id { get; set; }

    // Mã định danh tĩnh
    public string Code { get; set; } = string.Empty;

    // Thứ tự hiển thị sắp xếp trong danh sách quản lý
    public int SortOrder { get; set; }

    // Tiêu đề tiếng Việt
    public string TitleVi { get; set; } = string.Empty;

    // Tiêu đề tiếng Anh
    public string TitleEn { get; set; } = string.Empty;

    // Tóm tắt mô tả tiếng Việt
    public string DescVi { get; set; } = string.Empty;

    // Số tầng đặt hiện vật
    public int? Floor { get; set; }

    // Tọa độ vĩ độ phẳng phục vụ vẽ marker lên bản đồ
    public double Lat { get; set; }

    // Tọa độ kinh độ phẳng phục vụ vẽ marker lên bản đồ
    public double Lng { get; set; }

    // Đường dẫn ảnh minh họa
    public string? ImageUrl { get; set; }

    // Trạng thái mở cửa phục vụ
    public bool IsActive { get; set; }

    // Tổng số lượt khách đã quét mã QR của địa điểm này
    public int ScanCount { get; set; }
}