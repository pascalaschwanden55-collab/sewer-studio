namespace NightlySoakRunner;

public sealed class NightlySoakRunService(
    ISoakProbe probe,
    IResourceSampler resourceSampler,
    ISoakDelay? delay = null)
{
    private readonly ISoakDelay _delay = delay ?? new SystemSoakDelay();

    public async Task<SoakRunResult> RunAsync(NightlySoakOptions options, CancellationToken ct = default)
    {
        var runStarted = DateTimeOffset.UtcNow;
        var durations = new List<double>();
        ResourceSnapshot? baseline = null;
        var completedRounds = 0;

        await using var csv = await SoakCsvWriter.CreateAsync(options.CsvPath, ct);
        for (var round = 1; ; round++)
        {
            ct.ThrowIfCancellationRequested();
            if (options.MaxRounds is int maxRounds && round > maxRounds)
                return Passed(completedRounds, options.CsvPath);
            if (round > 1 && DateTimeOffset.UtcNow - runStarted >= options.Duration)
                return Passed(completedRounds, options.CsvPath);

            var video = options.VideoPaths[(round - 1) % options.VideoPaths.Count];
            var roundStarted = DateTimeOffset.UtcNow;
            SoakProbeResult probeResult;
            ResourceSnapshot? snapshot = null;
            string? failure = null;

            try
            {
                probeResult = await probe.RunAsync(video, round, ct);
                if (!probeResult.Success)
                {
                    failure = string.IsNullOrWhiteSpace(probeResult.Error)
                        ? "Der KI-Vertragstest ist fehlgeschlagen."
                        : probeResult.Error;
                }
                else
                {
                    var pid = options.MonitorProcessId ?? probeResult.ProcessId
                        ?? throw new InvalidOperationException(
                            "Der Sidecar meldet keine Prozess-ID; Ressourcenmessung ist nicht moeglich.");
                    snapshot = await resourceSampler.CaptureAsync(
                        pid,
                        probeResult.HealthVramMb,
                        options,
                        ct);
                    durations.Add(probeResult.Elapsed.TotalMilliseconds);
                    failure = CheckLimits(options, round, durations, baseline, snapshot);
                    if (round > options.WarmupRounds && baseline is null)
                        baseline = snapshot;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                probeResult = new SoakProbeResult(false, DateTimeOffset.UtcNow - roundStarted, null, null, ex.Message);
                failure = ex.Message;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var success = failure is null;
            await csv.WriteAsync(new SoakRoundRecord(
                round,
                roundStarted,
                video,
                success,
                probeResult.Elapsed.TotalMilliseconds,
                snapshot?.ProcessId ?? probeResult.ProcessId,
                snapshot?.PrivateMemoryMb,
                snapshot?.HandleCount,
                snapshot?.HealthVramMb ?? probeResult.HealthVramMb,
                snapshot?.NvidiaVramMb,
                failure), ct);

            completedRounds++;
            Console.WriteLine(
                $"Runde {round}: {(success ? "OK" : "FEHLER")}, "
                + $"{probeResult.Elapsed.TotalMilliseconds:0} ms, "
                + $"RAM {snapshot?.PrivateMemoryMb:0} MB, Handles {snapshot?.HandleCount}");

            if (!success)
                return new SoakRunResult(false, completedRounds, options.CsvPath, failure!);

            if (options.MaxRounds is int finalRound && round >= finalRound)
                return Passed(completedRounds, options.CsvPath);
            if (DateTimeOffset.UtcNow - runStarted >= options.Duration)
                return Passed(completedRounds, options.CsvPath);

            await _delay.WaitAsync(options.Interval, ct);
        }
    }

    public static double Percentile95(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
            throw new ArgumentException("Mindestens ein Messwert ist erforderlich.", nameof(values));

        var index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static string? CheckLimits(
        NightlySoakOptions options,
        int round,
        IReadOnlyCollection<double> durations,
        ResourceSnapshot? baseline,
        ResourceSnapshot current)
    {
        if (current.PrivateMemoryMb > options.MaxPrivateMemoryMb)
            return $"RAM-Obergrenze ueberschritten: {current.PrivateMemoryMb:0.###} MB > {options.MaxPrivateMemoryMb:0.###} MB.";
        if (current.HandleCount > options.MaxHandles)
            return $"Handle-Obergrenze ueberschritten: {current.HandleCount} > {options.MaxHandles}.";

        var measuredVram = current.NvidiaVramMb ?? current.HealthVramMb;
        if (measuredVram > options.MaxVramMb)
            return $"GPU-Speichergrenze ueberschritten: {measuredVram:0.###} MB > {options.MaxVramMb:0.###} MB.";

        if (baseline is not null)
        {
            var memoryGrowth = current.PrivateMemoryMb - baseline.PrivateMemoryMb;
            if (memoryGrowth > options.MaxMemoryGrowthMb)
                return $"RAM-Wachstum ueberschritten: {memoryGrowth:0.###} MB > {options.MaxMemoryGrowthMb:0.###} MB.";

            var handleGrowth = current.HandleCount - baseline.HandleCount;
            if (handleGrowth > options.MaxHandleGrowth)
                return $"Handle-Wachstum ueberschritten: {handleGrowth} > {options.MaxHandleGrowth}.";
        }

        if (round >= 5)
        {
            var p95 = Percentile95(durations);
            if (p95 > options.MaxP95Milliseconds)
                return $"95%-Laufzeit ueberschritten: {p95:0.###} ms > {options.MaxP95Milliseconds:0.###} ms.";
        }

        return null;
    }

    private static SoakRunResult Passed(int rounds, string csvPath) => new(
        true,
        rounds,
        csvPath,
        $"Nachtlauf nach {rounds} Runde(n) ohne Grenzwertverletzung beendet.");
}
