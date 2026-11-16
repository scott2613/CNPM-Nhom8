using System.Security.Cryptography;

namespace AudioGuide.LocalDevelopment;

// Quản lý khóa bí mật (API key, password hash) trên máy local khi chạy môi trường Development
internal static class LocalAdminSecrets
{
    // Lưu file cấu hình tạm trong thư mục LocalApplicationData của hệ điều hành (tránh lộ lên Git)
    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioGuide", "LocalDevelopment-v1");

    // Đọc API key dùng chung giữa Admin và API; tự tạo mới chuỗi hex 32 byte nếu chưa có
    internal static string ReadOrCreateApiKey()
    {
        var path = Path.Combine(DirectoryPath, "api-key");
        if (!File.Exists(path))
            TryWrite(path, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return File.ReadAllText(path).Trim();
    }

    // Đọc chuỗi hash mật khẩu admin lưu trên máy local (trả về null nếu chưa khởi tạo)
    internal static string? ReadPasswordHash()
    {
        var path = Path.Combine(DirectoryPath, "admin-password-hash");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    // Ghi file an toàn qua file tạm để tránh xung đột khi Admin và API khởi động cùng lúc
    private static bool TryWrite(string path, string value)
    {
        Directory.CreateDirectory(DirectoryPath);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var temporary = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N"));
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var writer = new StreamWriter(new FileStream(temporary, options)))
                writer.Write(value);
            try { File.Move(temporary, path); return true; }
            catch (IOException) when (File.Exists(path)) { return false; }
        }
        finally { File.Delete(temporary); }
    }
}