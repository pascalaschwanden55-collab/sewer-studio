using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Diagnostics;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Kapselt den Pipe-Axis-Loop (Ringbuffer + <see cref="PipeAxisBendDetector"/>
/// + Push in den <see cref="IAiDiagnosticsRecorder"/>). Aus dem PlayerWindow
/// migriert (Audit 2026-05-13 H4) — UI ruft nur noch <see cref="PushFrameAsync"/>
/// pro analysiertem Frame.
///
/// Der Service trifft keine Persistenz-Entscheidungen: Er schreibt die
/// Diagnose-Spur und gibt Bend-Kandidaten an die UI zurueck.
///
/// Lebenszyklus: instanzweise pro Codier-Session (Ringbuffer ist Session-State).
/// Aufrufseite ist single-threaded (Codier-Loop); ein Mini-Lock schuetzt den
/// Buffer dennoch, falls kuenftig parallele Push-Aufrufe noetig werden.
/// </summary>
public sealed class PipeAxisDiagnosticsService
{
    private readonly Func<PipeAxisRequest, CancellationToken, Task<PipeAxisResult?>> _analyze;
    private readonly IAiDiagnosticsRecorder _recorder;
    private readonly PipeAxisBendDetector _detector;
    private readonly object _bufferLock = new();
    private readonly Queue<(PipeAxisResult Axis, double Position)> _history = new();

    /// <summary>Maximale Anzahl Samples im Ringbuffer (Default 8).</summary>
    public int HistoryMaxSize { get; }

    public PipeAxisDiagnosticsService(
        Func<PipeAxisRequest, CancellationToken, Task<PipeAxisResult?>> analyze,
        IAiDiagnosticsRecorder recorder,
        PipeAxisBendDetector? detector = null,
        int historyMaxSize = 8)
    {
        _analyze = analyze ?? throw new ArgumentNullException(nameof(analyze));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _detector = detector ?? new PipeAxisBendDetector();
        if (historyMaxSize < 1)
            throw new ArgumentOutOfRangeException(nameof(historyMaxSize));
        HistoryMaxSize = historyMaxSize;
    }

    /// <summary>
    /// Verarbeitet einen Frame: Sidecar-Analyse → Ringbuffer-Update → bei
    /// erreichter Fenstergroesse Detektor-Aufruf + Diagnose-Record.
    /// Wirft nicht; Fehler werden geschluckt und auf <c>Debug.WriteLine</c>
    /// geschrieben (Diagnose-Pfad darf den Codier-Loop nie kippen).
    /// </summary>
    public async Task<PipeAxisBendDetector.BendDetectionResult?> PushFrameAsync(
        string framePngBase64,
        double captureTimestampSec,
        int? pipeDiameterMm,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(framePngBase64)) return null;

        PipeAxisResult? axis;
        try
        {
            axis = await _analyze(
                new PipeAxisRequest(framePngBase64, pipeDiameterMm), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PipeAxis] Fehler: {ex.Message}");
            return null;
        }

        if (axis == null) return null;

        List<(PipeAxisResult Axis, double Position)> samples;
        lock (_bufferLock)
        {
            _history.Enqueue((axis, captureTimestampSec));
            while (_history.Count > HistoryMaxSize)
                _history.Dequeue();

            if (_history.Count < _detector.MinWindowSize) return null;
            samples = _history.ToList();
        }

        var result = _detector.Detect(samples);

        _recorder.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.PipeAxisGeometry,
            Source = nameof(PipeAxisDiagnosticsService),
            Summary = result.DiagnosticText,
            LatencyMs = axis.InferenceTimeMs,
            Metadata = new Dictionary<string, string>
            {
                ["window"] = result.WindowSize.ToString(CultureInfo.InvariantCulture),
                ["curvature"] = result.CurvatureScore.ToString("F3", CultureInfo.InvariantCulture),
                ["direction"] = result.Direction.ToString(),
                ["confidence"] = result.Confidence.ToString("F2", CultureInfo.InvariantCulture),
                ["recommended_code"] = result.RecommendedCode ?? "",
                ["last_vx"] = axis.VanishingX.ToString("F3", CultureInfo.InvariantCulture),
                ["last_vy"] = axis.VanishingY.ToString("F3", CultureInfo.InvariantCulture)
            }
        });

        return result.RecommendedCode is null ? null : result;
    }

    /// <summary>Aktuelle Buffer-Groesse (fuer Diagnose/Tests).</summary>
    public int BufferedSampleCount
    {
        get { lock (_bufferLock) return _history.Count; }
    }

    /// <summary>Leert den Ringbuffer (z.B. beim Haltungswechsel).</summary>
    public void Reset()
    {
        lock (_bufferLock) _history.Clear();
    }
}
