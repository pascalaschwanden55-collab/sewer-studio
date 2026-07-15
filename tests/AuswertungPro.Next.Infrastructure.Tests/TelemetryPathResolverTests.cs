using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Telemetry;
using AuswertungPro.Next.Infrastructure.Vsa;

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

    [Fact]
    public void Telemetrie_Schreiber_verwenden_injizierten_Pfadaufloeser()
    {
        var paths = new RecordingTelemetryPathResolver();

        Assert.Equal("sidecar.jsonl", new SidecarTelemetryFileWriter(paths).ResolvePath());
        Assert.Equal("pipeline_trace_run-23.jsonl", new PipelineTraceFileWriter(paths).ResolvePath("run-23"));
        Assert.Equal("pipeline_summary_run-23.json", new PipelineTraceFileWriter(paths).ResolveSummaryPath("run-23"));
        Assert.Equal("vsa_shadow.jsonl", new VsaShadowTelemetryFileWriter(paths).ResolvePath());

        Assert.Equal(
            [
                "sidecar.jsonl",
                "pipeline_trace_run-23.jsonl",
                "pipeline_summary_run-23.json",
                "vsa_shadow.jsonl"
            ],
            paths.FileNames);
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

    private sealed class RecordingTelemetryPathResolver : ITelemetryPathResolver
    {
        public List<string> FileNames { get; } = [];

        public string? ResolveFile(string fileName)
        {
            FileNames.Add(fileName);
            return fileName;
        }
    }
}
