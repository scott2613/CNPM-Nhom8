namespace AudioGuide.Core.Entities;

public sealed class AdminAccount
{
    public const int SingleAccountId = 1;

    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid Version { get; set; }
}
