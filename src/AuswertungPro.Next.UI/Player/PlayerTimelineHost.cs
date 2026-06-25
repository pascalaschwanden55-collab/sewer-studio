namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerTimelineHost
{
    private readonly Func<long?> _readTimeMilliseconds;
    private readonly Func<long?> _readLengthMilliseconds;
    private readonly Action<long> _seekMilliseconds;

    public PlayerTimelineHost(
        Func<long?> readTimeMilliseconds,
        Func<long?> readLengthMilliseconds,
        Action<long> seekMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(readTimeMilliseconds);
        ArgumentNullException.ThrowIfNull(readLengthMilliseconds);
        ArgumentNullException.ThrowIfNull(seekMilliseconds);

        _readTimeMilliseconds = readTimeMilliseconds;
        _readLengthMilliseconds = readLengthMilliseconds;
        _seekMilliseconds = seekMilliseconds;
    }

    public long? TimeMilliseconds => _readTimeMilliseconds();

    public long? LengthMilliseconds => _readLengthMilliseconds();

    public double? CurrentSeconds => TimeMilliseconds / 1000.0;

    public double? DurationSeconds => LengthMilliseconds / 1000.0;

    public TimeSpan CurrentTimeOrZero => TimeSpan.FromMilliseconds(CurrentMillisecondsOrZero());

    public double CurrentSecondsOrZero => CurrentMillisecondsOrZero() / 1000.0;

    public void SeekMilliseconds(long milliseconds)
        => _seekMilliseconds(milliseconds);

    private double CurrentMillisecondsOrZero()
        => Math.Max(0, TimeMilliseconds ?? 0);
}
