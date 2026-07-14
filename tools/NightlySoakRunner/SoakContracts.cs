namespace NightlySoakRunner;

public sealed record SoakProbeResult(
    bool Success,
    TimeSpan Elapsed,
    int? ProcessId,
    double? HealthVramMb,
    string? Error = null);

public sealed record ResourceSnapshot(
    int ProcessId,
    double PrivateMemoryMb,
    int HandleCount,
    double? HealthVramMb,
    double? NvidiaVramMb);

public sealed record SoakRoundRecord(
    int Round,
    DateTimeOffset StartedUtc,
    string VideoPath,
    bool Success,
    double ElapsedMilliseconds,
    int? ProcessId,
    double? PrivateMemoryMb,
    int? HandleCount,
    double? HealthVramMb,
    double? NvidiaVramMb,
    string? Error);

public sealed record SoakRunResult(
    bool Success,
    int CompletedRounds,
    string CsvPath,
    string Message);

public interface ISoakProbe
{
    Task<SoakProbeResult> RunAsync(string videoPath, int round, CancellationToken ct);
}

public interface IResourceSampler
{
    Task<ResourceSnapshot> CaptureAsync(
        int processId,
        double? healthVramMb,
        NightlySoakOptions options,
        CancellationToken ct);
}

public interface ISoakDelay
{
    Task WaitAsync(TimeSpan duration, CancellationToken ct);
}

public sealed class SystemSoakDelay : ISoakDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken ct) => Task.Delay(duration, ct);
}
