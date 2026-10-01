using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Testhilfen fuer den Referenzvergleich der Mehrmodell-Analyse (AP05): Ein gemeinsames,
/// geordnetes Ereignisprotokoll haelt Modellaufrufe, Trace, Checkpoint, Fortschritt und
/// Logmeldungen in genau der Reihenfolge fest, in der der Lauf sie erzeugt. Zeitmessungen,
/// Lauf-Kennung und Zeitstempel bleiben bewusst draussen, damit der Vergleich zeichengleich
/// wiederholbar ist.
/// </summary>
internal sealed class MultiModelSnapshotRecorder :
    IPipelineTraceWriter, IAnalysisCheckpointJournal, IProgress<VideoAnalysisProgress>, ILogger
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public List<string> Events { get; } = new();

    /// <summary>Optionaler Fehler beim Anhaengen eines Checkpoint-Frames (Frame-Index -> Ausnahme).</summary>
    public Func<AnalysisCheckpointFrame, Exception?>? AppendFailure { get; init; }

    /// <summary>Resume-Stand, den <see cref="OpenAsync"/> liefert (Standard: leer).</summary>
    public AnalysisCheckpointState OpenState { get; init; } = AnalysisCheckpointState.Empty;

    public static string ToJson(object? value) => JsonSerializer.Serialize(value, Json);

    public void Add(string line) => Events.Add(line);

    // ── Trace ────────────────────────────────────────────────────────────────
    public Task WriteAsync(PipelineTraceEntry entry)
    {
        var copy = new PipelineTraceEntry
        {
            FrameIndex = entry.FrameIndex, TimeSec = entry.TimeSec, Meter = entry.Meter, Path = entry.Path,
            YoloBypass = entry.YoloBypass, YoloRelevant = entry.YoloRelevant,
            YoloDetectionCount = entry.YoloDetectionCount, DinoBoxCount = entry.DinoBoxCount,
            SamMaskCount = entry.SamMaskCount, FindingsBuilt = entry.FindingsBuilt,
            CodesFromLabel = entry.CodesFromLabel, ClassifierCode = entry.ClassifierCode,
            ClassifierConfidence = entry.ClassifierConfidence, ClassifierSource = entry.ClassifierSource,
            ClassifierModel = entry.ClassifierModel, ClassifierVoteConfirmed = entry.ClassifierVoteConfirmed,
            QwenCalled = entry.QwenCalled, QwenImageQuality = entry.QwenImageQuality,
            QwenRawFindingCount = entry.QwenRawFindingCount, CodesAfterQwen = entry.CodesAfterQwen,
            FindingsEndOfFrame = entry.FindingsEndOfFrame, ActiveCount = entry.ActiveCount,
            DetectionsTotal = entry.DetectionsTotal, DropReason = entry.DropReason,
            Degraded = entry.Degraded, DegradedReason = entry.DegradedReason,
            OsdMeterRejected = entry.OsdMeterRejected,
        };
        Events.Add("TRACE " + ToJson(copy));
        return Task.CompletedTask;
    }

    public Task WriteSummaryAsync(string runId, TelemetrySummary summary)
    {
        Events.Add($"SUMMARY frames={summary.TotalFrames} skipped={summary.SkippedFrames} failed={summary.FailedFrames}");
        return Task.CompletedTask;
    }

    public string? ResolvePath(string runId) => null;
    public string? ResolveSummaryPath(string runId) => null;

    // ── Checkpoint ───────────────────────────────────────────────────────────
    public Task<AnalysisCheckpointState> OpenAsync(string videoPath, double stepSeconds, CancellationToken ct = default)
    {
        Events.Add($"CKPT-OPEN step={stepSeconds.ToString("R", CultureInfo.InvariantCulture)}");
        return Task.FromResult(OpenState);
    }

    public Task AppendFrameAsync(AnalysisCheckpointFrame frame, CancellationToken ct = default)
    {
        Events.Add("CKPT " + ToJson(frame));
        if (AppendFailure?.Invoke(frame) is { } failure)
            throw failure;
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken ct = default)
    {
        Events.Add("CKPT-COMPLETE");
        return Task.CompletedTask;
    }

    // ── Fortschritt ──────────────────────────────────────────────────────────
    public void Report(VideoAnalysisProgress value)
        => Events.Add($"PROG {value.FramesDone}/{value.FramesTotal} preview={value.FramePreviewPng?.Length ?? -1} "
                      + $"{value.Status} live={ToJson(value.LiveFindings)}");

    // ── Log ──────────────────────────────────────────────────────────────────
    IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Lauf-Kennung und Messzeiten sind je Lauf verschieden und gehoeren nicht in den Vergleich.
        var line = $"LOG {logLevel} {formatter(state, exception)}"
                   + (exception is null ? string.Empty : $" [{exception.GetType().Name}: {exception.Message}]");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"runId=\d{8}_\d{6}_[0-9a-f]{6}", "runId=<lauf>");
        line = System.Text.RegularExpressions.Regex.Replace(line, @"=\d+ms", "=<zeit>ms");
        // Der Journalname ist ein Hash des vollen Videopfads und haengt damit am Arbeitsverzeichnis
        // (Debug- und Release-Ausgabe liegen in verschiedenen Ordnern).
        line = System.Text.RegularExpressions.Regex.Replace(line, @"analysis_checkpoint_[0-9a-f]{16}", "analysis_checkpoint_<hash>");
        Events.Add(line);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    /// <summary>Schreibt das Ergebnis und alle Ereignisse als festen Text.</summary>
    public string Render(VideoAnalysisResult? result, Exception? thrown = null)
    {
        var sb = new StringBuilder();
        foreach (var line in Events)
            sb.Append(line).Append('\n');
        if (thrown is not null)
            sb.Append("THROWN ").Append(thrown.GetType().Name).Append(": ").Append(thrown.Message).Append('\n');
        if (result is not null)
        {
            var telemetry = result.Telemetry;
            sb.Append("RESULT ").Append(ToJson(result with { Telemetry = null, Detections = Array.Empty<RawVideoDetection>(), VideoPath = "<pfad>" })).Append('\n');
            sb.Append($"TELEMETRY frames={telemetry?.TotalFrames} skipped={telemetry?.SkippedFrames} failed={telemetry?.FailedFrames}\n");
            foreach (var d in result.Detections)
                sb.Append("DETECTION ").Append(ToJson(d)).Append('\n');
        }
        return sb.ToString();
    }
}

/// <summary>Frames mit angehaengtem Kennbyte: Der Fake-Client antwortet je Bild, nicht je Aufruf.</summary>
internal static class SnapshotFrames
{
    private static readonly byte[] MinPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC, 0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public static byte[] Marked(int marker)
    {
        var bytes = new byte[MinPng.Length + 1];
        MinPng.CopyTo(bytes, 0);
        bytes[^1] = (byte)marker;
        return bytes;
    }

    public static int MarkerOf(string imageBase64) => Convert.FromBase64String(imageBase64)[^1];

    public static async IAsyncEnumerable<FrameData> Source(int count, ISet<int>? emptyMarkers,
        Action<int>? beforeYield, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            beforeYield?.Invoke(i);
            yield return new FrameData(i * 1.0, emptyMarkers?.Contains(i) == true ? Array.Empty<byte>() : Marked(i));
            await Task.Yield();
        }
    }
}

/// <summary>Skriptbarer Sidecar-Fake: jede Antwort haengt am Kennbyte des Bildes.</summary>
internal sealed class ScriptedVisionClient : IVisionPipelineClient
{
    private readonly MultiModelSnapshotRecorder _recorder;

    public ScriptedVisionClient(MultiModelSnapshotRecorder recorder) => _recorder = recorder;

    public SidecarDetectorQualification? Qualification { get; init; } = new(true, null);
    public Func<int, YoloClassifyResponse> Cls { get; init; } = _ => new([], 1);
    public Func<int, YoloResponse> Yolo { get; init; } = _ => HealthyYolo();
    public Func<int, DinoResponse> Dino { get; init; } = _ => TwoBoxes();
    public Func<int, SamResponse> Sam { get; init; } = _ => TwoMasks();

    public static YoloResponse HealthyYolo(double? crackConf = null, string model = "yolo26m.pt", bool? qualified = true)
        => new(true,
            crackConf is { } c ? [new YoloDetectionDto(10, 10, 80, 80, "crack", c)] : [],
            "damage", 1, ModelName: model, DetectorQualified: qualified,
            DetectorQualificationReason: qualified == true ? null : "Freigabe im Lauf entzogen (Test).");

    public static DinoResponse TwoBoxes() => new([
        new DinoDetectionDto(10, 10, 80, 80, "crack", 0.8, "crack"),
        new DinoDetectionDto(540, 350, 610, 420, "roots", 0.7, "roots")], 1);

    public static DinoResponse NoBoxes() => new([], 1);

    public static SamMaskResult Mask(string label, double x, double y) => new(label, 0.8,
        [x, y, x + 70, y + 70], "", 4900, 640 * 480, 70, 70, x + 35, y + 35);

    public static SamResponse TwoMasks() => new([Mask("crack", 10, 10), Mask("roots", 540, 350)], 640, 480, 1);

    public static SidecarInsufficientVramException Vram(string endpoint) => new(endpoint, 1.5, 6.0, 2.0);

    public static HttpRequestException Transport() => new("Sidecar nicht erreichbar (Test).");

    public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
    {
        _recorder.Add("CALL health");
        return Task.FromResult<SidecarHealthResponse?>(
            new SidecarHealthResponse("ok", "test", null, DetectorQualification: Qualification));
    }

    public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
        => Task.FromResult(new PipelineHealthCheckResult(true, true, 200, null, null));

    public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
        => Answer("cls", request.ImageBase64, Cls);

    public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
    {
        _recorder.Add($"CALL yolo-conf={request.ConfidenceThreshold.ToString("R", CultureInfo.InvariantCulture)}");
        return Answer("yolo", request.ImageBase64, Yolo);
    }

    public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
        => Answer("dino", request.ImageBase64, Dino);

    public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
    {
        _recorder.Add($"CALL sam-boxes={request.BoundingBoxes.Count} dn={request.PipeDiameterMm}");
        return Answer("sam", request.ImageBase64, Sam);
    }

    private Task<T> Answer<T>(string step, string imageBase64, Func<int, T> script)
    {
        var marker = SnapshotFrames.MarkerOf(imageBase64);
        _recorder.Add($"CALL {step} m={marker}");
        return Task.FromResult(script(marker));
    }
}

/// <summary>Qwen/Ollama-Antworten ohne Netz: Der Handler beantwortet die n-te Anfrage.</summary>
internal sealed class ScriptedQwenHandler(Func<int, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(respond(++Calls, ct));

    public static HttpResponseMessage Content(string contentJson) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { message = new { content = contentJson } }))
    };

    public static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable)
    {
        Content = new StringContent("synthetisch offline")
    };
}

internal sealed class FixedPipelineOptions : IPipelineEnvironmentOptions
{
    public bool ClassifierDecisionEnabled() => false;
    public bool ClassifierOnlyStructuralEnabled() => false;
    public string ExpectedYoloModel() => "yolo26m";
    public double? ReadDoubleWithCompat(string sewerStudioName) => null;
    public double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue) => defaultValue;
}

internal sealed class ScriptedRestartService(MultiModelSnapshotRecorder recorder, params SidecarRestartResult[] results)
    : ISidecarRestartService
{
    private int _calls;

    public Task<SidecarRestartResult> TryRestartAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        recorder.Add("CALL restart");
        return Task.FromResult(results[Math.Min(_calls++, results.Length - 1)]);
    }
}
