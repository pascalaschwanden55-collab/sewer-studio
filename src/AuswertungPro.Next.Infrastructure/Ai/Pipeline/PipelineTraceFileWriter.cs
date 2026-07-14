using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Telemetry;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Dateibasierte Best-Effort-Ausgabe des Pipeline-Ablaufs.</summary>
public sealed class PipelineTraceFileWriter : IPipelineTraceWriter
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(PipelineTraceEntry entry)
    {
        await BestEffort.TryAsync(
            async () =>
            {
                var path = ResolvePath(entry.RunId);
                if (path is null)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

                await WriteLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await File.AppendAllTextAsync(path, line).ConfigureAwait(false);
                }
                finally
                {
                    WriteLock.Release();
                }
            },
            $"PipelineTraceWriter Trace schreiben: {entry.RunId}").ConfigureAwait(false);
    }

    public async Task WriteSummaryAsync(string runId, TelemetrySummary summary)
    {
        await BestEffort.TryAsync(
            async () =>
            {
                var path = ResolveSummaryPath(runId);
                if (path is null)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(summary, JsonOptions);

                await WriteLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await AtomicTextFileWriter.WriteAllTextAsync(path, json).ConfigureAwait(false);
                }
                finally
                {
                    WriteLock.Release();
                }
            },
            $"PipelineTraceWriter Summary schreiben: {runId}").ConfigureAwait(false);
    }

    public string? ResolvePath(string runId)
        => ResolveFile(runId, "pipeline_trace_", ".jsonl");

    public string? ResolveSummaryPath(string runId)
        => ResolveFile(runId, "pipeline_summary_", ".json");

    private static string? ResolveFile(string runId, string prefix, string extension)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return null;

        foreach (var character in Path.GetInvalidFileNameChars())
            runId = runId.Replace(character, '_');

        return TelemetryPathResolver.ResolveFile($"{prefix}{runId}{extension}");
    }
}
