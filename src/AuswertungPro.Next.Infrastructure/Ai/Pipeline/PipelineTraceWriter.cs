using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Schreibt pro KI-Lauf eine Trace-Datei (JSONL) mit einem Eintrag je Frame.
/// Reine Sichtbarkeit fuer die Fehlersuche — aendert KEIN Pipeline-Verhalten.
/// Zeigt pro Frame, wie viele Befunde nach jeder Stufe (YOLO/DINO/SAM/Qwen/
/// Validierung/Dedup) uebrig bleiben und WARUM etwas verworfen wurde.
/// Datei: %LOCALAPPDATA%/SewerStudio/Telemetry/pipeline_trace_{runId}.jsonl
/// (Spiegelt das Muster von <see cref="SidecarTelemetryWriter"/>.)
/// </summary>
public static class PipelineTraceWriter
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(PipelineFrameTrace entry)
    {
        try
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
        }
        catch
        {
            // Trace darf die eigentliche Analyse niemals stoeren.
        }
    }

    public static string? ResolvePath(string runId)
    {
        // RunId fliesst in den Dateinamen — gegen Path-Traversal absichern
        // (RunId ist ein public settable Property; "..\\.." o.ae. darf nicht durchschlagen).
        if (string.IsNullOrWhiteSpace(runId))
            return null;
        foreach (var c in Path.GetInvalidFileNameChars())
            runId = runId.Replace(c, '_');

        var overrideDir = Environment.GetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR");
        var root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, "SewerStudio", "Telemetry", $"pipeline_trace_{runId}.jsonl");
    }
}

/// <summary>
/// Ein Trace-Eintrag pro Frame. Mutable, weil er waehrend der Frame-Verarbeitung
/// stufenweise befuellt und am Ende (bzw. am jeweiligen Abbruchpunkt) einmal geschrieben wird.
/// </summary>
public sealed class PipelineFrameTrace
{
    public string RunId { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; }
    public int FrameIndex { get; set; }
    public double TimeSec { get; set; }
    public double Meter { get; set; }

    /// <summary>Welcher Pfad der Frame genommen hat: processed / empty_frame / yolo_cls_skip / yolo_error / yolo_irrelevant / dino_error / dino_no_boxes / sam_error.</summary>
    public string Path { get; set; } = "processed";

    public bool YoloBypass { get; set; }
    public bool? YoloRelevant { get; set; }
    public int YoloDetectionCount { get; set; }
    public int DinoBoxCount { get; set; }
    public int SamMaskCount { get; set; }

    /// <summary>Befunde aus SAM-Masken gebaut (vor Qwen).</summary>
    public int FindingsBuilt { get; set; }
    /// <summary>Codes vor Validierung/Qwen: aus dem DINO-Label abgeleitete VSA-Codes.</summary>
    public int CodesFromLabel { get; set; }

    public bool QwenCalled { get; set; }
    public string? QwenImageQuality { get; set; }
    public int QwenRawFindingCount { get; set; }
    /// <summary>Codes nach Qwen-Anreicherung (Befunde mit nicht-leerem VSA-Code).</summary>
    public int CodesAfterQwen { get; set; }

    /// <summary>Befunde am Frame-Ende (nach evtl. ImageQuality-Clear).</summary>
    public int FindingsEndOfFrame { get; set; }
    /// <summary>Aktive Befunde im Tracking/Dedup-Puffer nach diesem Frame.</summary>
    public int ActiveCount { get; set; }
    /// <summary>Bisher abgeschlossene Detections (laufende Summe).</summary>
    public int DetectionsTotal { get; set; }

    /// <summary>Grund, falls (Teil-)Befunde verloren gehen: empty_frame, yolo_cls_normal, yolo_error, yolo_irrelevant, dino_error, dino_no_boxes, sam_error, image_quality_bad, all_findings_missing_code, no_findings.</summary>
    public string? DropReason { get; set; }
}
