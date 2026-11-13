namespace AudioGuide.Accounts;

internal static class AdminAccountRules
{
    public const int MinimumPasswordLength = 4;
    public const int MaximumPasswordLength = 1024;
    public const int MaximumUsernameLength = 100;
    public const string VersionClaim = "audioguide:account-version";

    public static bool IsUsernameValid(string? value) =>
        value is { Length: > 0 and <= MaximumUsernameLength }
        && value == value.Trim() && !value.Any(char.IsControl);

    public static bool IsNewPasswordValid(string? value) =>
        value is { Length: >= MinimumPasswordLength and <= MaximumPasswordLength }
        && !string.IsNullOrWhiteSpace(value);
}

public sealed record AdminAccountStatus(string Username, Guid Version);
public sealed record AdminAccountSeed(string Username, string PasswordHash);
public sealed record AdminLoginRequest(string Username, string Password);
public sealed record AdminAccountChange(Guid Version, string CurrentPassword, string? NewUsername, string? NewPassword);
