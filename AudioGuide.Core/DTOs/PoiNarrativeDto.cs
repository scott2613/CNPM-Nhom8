<<<<<<< HEAD
﻿namespace AudioGuide.Core.DTOs;

// DTO trả về thông tin bài thuyết minh hiển thị lên trình phát của khách
public class PoiNarrativeDto
{
    // Mã ID định danh điểm tham quan
    public int PoiId { get; set; }

    // Mã code nghiệp vụ của điểm tham quan
    public string Code { get; set; } = string.Empty;

    // Tiêu đề địa điểm bằng ngôn ngữ đã chọn
    public string Title { get; set; } = string.Empty;

    // Nội dung bài thuyết minh dạng văn bản
    public string? Description { get; set; }

    // Đường dẫn file âm thanh mp3 sẵn sàng phát
    public string AudioUrl { get; set; } = string.Empty;

    // Thời lượng file âm thanh tính bằng giây
    public int DurationSeconds { get; set; }

    // Ngôn ngữ của nội dung bài thuyết minh
    public string LanguageCode { get; set; } = string.Empty;

    // Khoảng cách thực tế từ vị trí du khách đến điểm tham quan (đơn vị: mét)
    public double DistanceMeters { get; set; }
=======
﻿namespace AudioGuide.Core.DTOs;

// DTO trả về thông tin bài thuyết minh hiển thị lên trình phát của khách
public class PoiNarrativeDto
{
    // Mã ID định danh điểm tham quan
    public int PoiId { get; set; }

    // Mã code nghiệp vụ của điểm tham quan
    public string Code { get; set; } = string.Empty;

    // Tiêu đề địa điểm bằng ngôn ngữ đã chọn
    public string Title { get; set; } = string.Empty;

    // Nội dung bài thuyết minh dạng văn bản
    public string? Description { get; set; }

    // Đường dẫn file âm thanh mp3 sẵn sàng phát
    public string AudioUrl { get; set; } = string.Empty;

    // Thời lượng file âm thanh tính bằng giây
    public int DurationSeconds { get; set; }

    // Ngôn ngữ của nội dung bài thuyết minh
    public string LanguageCode { get; set; } = string.Empty;

    // Khoảng cách thực tế từ vị trí du khách đến điểm tham quan (đơn vị: mét)
    public double DistanceMeters { get; set; }
>>>>>>> 0767c83 (Cập nhật lại admin)
}