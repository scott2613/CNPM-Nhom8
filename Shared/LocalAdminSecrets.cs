using System.Security.Cryptography;

namespace AudioGuide.LocalDevelopment;

internal static class LocalAdminSecrets
{
    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioGuide", "LocalDevelopment-v1");

    internal static string ReadOrCreateApiKey()
    {
        var path = Path.Combine(DirectoryPath, "api-key");
        if (!File.Exists(path))
            TryWrite(path, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return File.ReadAllText(path).Trim();
    }

    internal static string? ReadPasswordHash()
    {
        var path = Path.Combine(DirectoryPath, "admin-password-hash");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

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
