using AuswertungPro.Next.Infrastructure.Telemetry;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TelemetryPathResolverTests
{
    [Fact]
    public void ResolveFile_uses_explicit_telemetry_root()
    {
        using var scope = new TelemetryEnvScope(@"C:\tmp\telemetry");

        var path = TelemetryPathResolver.ResolveFile("sidecar.jsonl");

        Assert.Equal(
            Path.Combine(@"C:\tmp\telemetry", "SewerStudio", "Telemetry", "sidecar.jsonl"),
            path);
    }

    [Fact]
    public void ResolveFile_falls_back_to_local_appdata_when_override_is_empty()
    {
        using var scope = new TelemetryEnvScope("  ");

        var path = TelemetryPathResolver.ResolveFile("vsa_shadow.jsonl");

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio",
                "Telemetry",
                "vsa_shadow.jsonl"),
            path);
    }

    [Fact]
    public void ResolveFile_rejects_empty_file_name()
    {
        using var scope = new TelemetryEnvScope(@"C:\tmp\telemetry");

        Assert.Null(TelemetryPathResolver.ResolveFile(""));
        Assert.Null(TelemetryPathResolver.ResolveFile("  "));
    }

    private sealed class TelemetryEnvScope : IDisposable
    {
        private readonly string? _previous;

        public TelemetryEnvScope(string? value)
        {
            _previous = Environment.GetEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar);
            Environment.SetEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(TelemetryPathResolver.TelemetryDirEnvVar, _previous);
        }
    }
}
