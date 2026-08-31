namespace Mendako.Core;

/// <summary>テストから時間を差し替えられるようにするための時計。</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    TimeZoneInfo LocalTimeZone { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
