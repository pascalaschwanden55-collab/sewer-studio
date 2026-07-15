using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Telemetry;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Dateibasierte Best-Effort-Ausgabe der Sidecar-Laufzeitdaten.</summary>
public sealed class SidecarTelemetryFileWriter : ISidecarTelemetryWriter
{
    private const long MaxBytes = 10L * 1024 * 1024;
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly ITelemetryPathResolver _paths;

    public SidecarTelemetryFileWriter()
        : this(TelemetryPathResolver.Current)
    {
    }

    public SidecarTelemetryFileWriter(ITelemetryPathResolver paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task WriteAsync(SidecarTelemetryEntry entry)
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
                RotateIfTooLarge(path);
                await File.AppendAllTextAsync(path, line).ConfigureAwait(false);
            }
            finally
            {
                WriteLock.Release();
            }
        }
        catch
        {
            // Telemetrie darf die eigentliche Analyseanfrage nie beeinflussen.
        }
    }

    public string? ResolvePath()
        => _paths.ResolveFile("sidecar.jsonl");

    private static void RotateIfTooLarge(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < MaxBytes)
                return;

            var rolled = path + ".1";
            if (File.Exists(rolled))
                File.Delete(rolled);
            File.Move(path, rolled);
        }
        catch
        {
            // Rotation darf weder Schreiben noch Analyse kippen.
        }
    }
}
