using SidecarE2eSmoke;

namespace NightlySoakRunner;

public sealed class SidecarPipelineProbe(NightlySoakOptions options) : ISoakProbe
{
    public async Task<SoakProbeResult> RunAsync(string videoPath, int round, CancellationToken ct)
    {
        var smoke = options.CreateSmokeOptions(videoPath, startSidecar: false);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var report = await new SidecarSmokeRunner().RunAsync(smoke, ct);
        started.Stop();

        var error = report.Error;
        if (!report.Success && string.IsNullOrWhiteSpace(error))
        {
            error = report.GoldenValidation is { Success: false } golden
                ? string.Join(" | ", golden.Failures)
                : string.Join(" | ", report.Checks.Where(check => !check.Passed).Select(check => check.Detail));
        }

        return new SoakProbeResult(
            report.Success,
            started.Elapsed,
            report.Health?.ProcessId,
            report.Health?.VramAllocatedGb * 1024,
            error);
    }
}
