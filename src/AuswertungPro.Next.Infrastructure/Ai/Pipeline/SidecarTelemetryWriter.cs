using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

public static class SidecarTelemetryWriter
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static async Task WriteAsync(SidecarTelemetryEvent entry)
    {
        try
        {
            var path = ResolvePath();
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
        }
        catch
        {
            // Telemetry must never break the actual analysis request.
        }
    }

    public static string? ResolvePath()
    {
        var overrideDir = Environment.GetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR");
        var root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, "SewerStudio", "Telemetry", "sidecar.jsonl");
    }
}

public sealed record SidecarTelemetryEvent(
    DateTimeOffset TimestampUtc,
    string Endpoint,
    string? ModelName,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    int DetectionCount,
    bool? IsRelevant,
    string? FrameClass);
