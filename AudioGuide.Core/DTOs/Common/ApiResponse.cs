<<<<<<< HEAD
﻿namespace AudioGuide.Core.DTOs.Common;

// Đóng gói format dữ liệu trả về chuẩn toàn hệ thống cho mọi REST API
public class ApiResponse<T>
{
    // Cờ trạng thái request (true: thành công, false: thất bại)
    public bool Success { get; set; }

    // Thông báo kết quả thực hiện cho client
    public string Message { get; set; } = string.Empty;

    // Dữ liệu nghiệp vụ trả về dạng Generic
    public T? Data { get; set; }

    // Danh sách các thông điệp lỗi chi tiết nếu có sự cố
    public List<string>? Errors { get; set; }

    // Mã trạng thái HTTP tương ứng
    public int StatusCode { get; set; }

    // Phương thức tạo nhanh response trả về kết quả thành công kèm dữ liệu
    public static ApiResponse<T> Ok(T data, string message = "Thao tác thành công")
        => new() { Success = true, Data = data, Message = message, StatusCode = 200 };

    // Phương thức tạo nhanh response trả về kết quả lỗi
    public static ApiResponse<T> Fail(string message, int statusCode = 400, List<string>? errors = null)
        => new() { Success = false, Message = message, StatusCode = statusCode, Errors = errors };
=======
﻿namespace AudioGuide.Core.DTOs.Common;

// Đóng gói format dữ liệu trả về chuẩn toàn hệ thống cho mọi REST API
public class ApiResponse<T>
{
    // Cờ trạng thái request (true: thành công, false: thất bại)
    public bool Success { get; set; }

    // Thông báo kết quả thực hiện cho client
    public string Message { get; set; } = string.Empty;

    // Dữ liệu nghiệp vụ trả về dạng Generic
    public T? Data { get; set; }

    // Danh sách các thông điệp lỗi chi tiết nếu có sự cố
    public List<string>? Errors { get; set; }

    // Mã trạng thái HTTP tương ứng
    public int StatusCode { get; set; }

    // Phương thức tạo nhanh response trả về kết quả thành công kèm dữ liệu
    public static ApiResponse<T> Ok(T data, string message = "Thao tác thành công")
        => new() { Success = true, Data = data, Message = message, StatusCode = 200 };

    // Phương thức tạo nhanh response trả về kết quả lỗi
    public static ApiResponse<T> Fail(string message, int statusCode = 400, List<string>? errors = null)
        => new() { Success = false, Message = message, StatusCode = statusCode, Errors = errors };
>>>>>>> 0767c83 (Cập nhật lại admin)
}