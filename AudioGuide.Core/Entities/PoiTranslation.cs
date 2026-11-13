namespace AudioGuide.Core.Entities;

// Thực thể lưu trữ bản dịch thuyết minh theo từng ngôn ngữ
public class PoiTranslation
{
    // Khóa chính tự tăng của bản dịch
    public int Id { get; set; }

    // Khóa ngoại trỏ đến bảng Poi cha
    public int PoiId { get; set; }

    // Mã ngôn ngữ (ví dụ: vi, en, zh, fr, ru)
    public string LanguageCode { get; set; } = string.Empty;

    // Tiêu đề địa điểm sau khi dịch
    public string Title { get; set; } = string.Empty;

    // Nội dung văn bản thuyết minh chi tiết
    public string? Description { get; set; }

    // Đường dẫn file âm thanh mp3 đọc bài thuyết minh
    public string AudioUrl { get; set; } = string.Empty;

    // Thời lượng file âm thanh tính theo giây
    public int DurationSeconds { get; set; } = 30;

    // Liên kết ngược về thực thể Poi cha
    public Poi? Poi { get; set; }
}