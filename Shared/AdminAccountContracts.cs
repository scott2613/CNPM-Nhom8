namespace AudioGuide.Accounts;

// Quy định ràng buộc dữ liệu và claim xác thực cho tài khoản admin
internal static class AdminAccountRules
{
    public const int MinimumPasswordLength = 4;
    public const int MaximumPasswordLength = 1024;
    public const int MaximumUsernameLength = 100;
    public const string VersionClaim = "audioguide:account-version";

    // Kiểm tra username: 1-100 ký tự, không khoảng trắng đầu/cuối và không chứa ký tự điều khiển
    public static bool IsUsernameValid(string? value) =>
        value is { Length: > 0 and <= MaximumUsernameLength }
        && value == value.Trim() && !value.Any(char.IsControl);

    // Kiểm tra mật khẩu mới: 4-1024 ký tự và không được toàn khoảng trắng
    public static bool IsNewPasswordValid(string? value) =>
        value is { Length: >= MinimumPasswordLength and <= MaximumPasswordLength }
        && !string.IsNullOrWhiteSpace(value);
}

// Các record DTO trao đổi dữ liệu tài khoản giữa Admin và API
public sealed record AdminAccountStatus(string Username, Guid Version);
public sealed record AdminAccountSeed(string Username, string PasswordHash);
public sealed record AdminLoginRequest(string Username, string Password);
public sealed record AdminAccountChange(Guid Version, string CurrentPassword, string? NewUsername, string? NewPassword);