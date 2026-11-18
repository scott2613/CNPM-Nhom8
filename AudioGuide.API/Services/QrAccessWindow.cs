namespace AudioGuide.API.Services;

// One shared admission window per API process. Reads do not extend the deadline.
public sealed class QrAccessWindow(TimeProvider clock)
{
    private readonly object sync = new();
    private DateTimeOffset? openedAt;
    public const int DurationSeconds = 60;

    public QrWindowStatus Status()
    {
        lock (sync)
            return Describe(openedAt, clock.GetUtcNow());
    }

    public QrWindowStatus Admit()
    {
        lock (sync)
        {
            openedAt ??= clock.GetUtcNow();
            return Describe(openedAt, clock.GetUtcNow());
        }
    }

    public QrWindowStatus Open()
    {
        lock (sync)
        {
            openedAt = clock.GetUtcNow();
            return Describe(openedAt, clock.GetUtcNow());
        }
    }

    public static QrWindowStatus Describe(DateTimeOffset? start, DateTimeOffset now)
    {
        var deadline = start?.AddSeconds(DurationSeconds);
        var allowed = deadline == null || now < deadline.Value;
        var remaining = deadline == null
            ? DurationSeconds
            : (int)Math.Clamp(Math.Ceiling((deadline.Value - now).TotalSeconds), 0, DurationSeconds);
        return new(start == null ? "Waiting" : allowed ? "Open" : "Expired",
            allowed, remaining, start, deadline, now);
    }
}

public sealed record QrWindowStatus(
    string State, bool CanAccess, int RemainingSeconds,
    DateTimeOffset? OpenedAtUtc, DateTimeOffset? ExpiresAtUtc, DateTimeOffset ServerTimeUtc);
