using System;
using AuswertungPro.Next.Application.Ai.Vision;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai;

// Phase 5.3 vorbereitend: KnickFinding nach Application/Ai/Vision/VideoAnalysisModels.cs.

/// <summary>
/// Knick-Erkennung: Trackt den Fluchtpunkt ueber Video-Frames
/// und erkennt abrupte Richtungswechsel an Rohrverbindungen.
///
/// Knick (BAG) ≠ Bogen (BCC):
/// - Knick: Abrupter Richtungswechsel an einer Muffe, ab 10° ein Schaden
/// - Bogen: Geplante Richtungsaenderung, gleichmaessig ueber viele Frames
///
/// Algorithmus:
/// 1. Pro Frame: Fluchtpunkt ermitteln (via Sidecar, ~5ms)
/// 2. Gleitender Mittelwert (Window) geglaettet
/// 3. Abrupter Sprung > Schwelle UND Muffe erkannt → Knick
/// 4. Winkel aus Shift relativ zum Rohrradius berechnen
/// </summary>
public sealed class KnickDetectionService
{
    // Konfigurations-Schwellen
    private const double MinKnickAngleDeg = 10.0;    // Mindestwinkel fuer Knick
    private const int SmoothingWindow = 5;            // Frames fuer gleitenden Mittelwert
    private const double MinConfidence = 0.3;         // Mindest-Konfidenz Fluchtpunkt
    private const int MinFramesBeforeDetection = 8;   // Mindest-Frames vor erster Erkennung
    private const double BogenMaxShiftPerFrame = 0.01; // Max-Shift pro Frame fuer Bogen (graduell)

    private readonly VisionPipelineClient _sidecar;
    private readonly ILogger? _logger;

    // Tracking-Zustand
    private readonly List<AxisSample> _history = new();
    private readonly List<KnickFinding> _findings = new();
    private double _lastKnickMeter = -999;

    /// <summary>Alle erkannten Knicke seit Reset.</summary>
    public IReadOnlyList<KnickFinding> Findings => _findings;

    public KnickDetectionService(VisionPipelineClient sidecar, ILogger? logger = null)
    {
        _sidecar = sidecar;
        _logger = logger;
    }

    /// <summary>Zustand zuruecksetzen (neue Haltung).</summary>
    public void Reset()
    {
        _history.Clear();
        _findings.Clear();
        _lastKnickMeter = -999;
    }

    /// <summary>
    /// Frame verarbeiten: Fluchtpunkt analysieren und Knick pruefen.
    /// Wird pro Frame waehrend der Video-Analyse aufgerufen.
    /// </summary>
    /// <param name="imageBase64">Frame als Base64.</param>
    /// <param name="meterPosition">Aktueller Meterstand im Video.</param>
    /// <param name="frameIndex">Frame-Index.</param>
    /// <param name="pipeDiameterMm">Rohrdurchmesser (optional).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>KnickFinding wenn Knick erkannt, sonst null.</returns>
    public async Task<KnickFinding?> ProcessFrameAsync(
        string imageBase64,
        double meterPosition,
        int frameIndex,
        int? pipeDiameterMm = null,
        CancellationToken ct = default)
    {
        PipeAxisResult axis;
        try
        {
            var request = new PipeAxisRequest(imageBase64, pipeDiameterMm);
            axis = await _sidecar.AnalyzePipeAxisAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Pipe-Axis Analyse fehlgeschlagen bei {Meter}m: {Error}",
                meterPosition, ex.Message);
            return null;
        }

        if (axis.Confidence < MinConfidence)
            return null;

        var sample = new AxisSample(
            frameIndex, meterPosition,
            axis.VanishingX, axis.VanishingY,
            axis.PipeRadiusX, axis.PipeRadiusY,
            axis.Confidence, axis.HasJoint);

        _history.Add(sample);

        // Nicht genug Daten fuer Erkennung
        if (_history.Count < MinFramesBeforeDetection)
            return null;

        // Knick pruefen
        var knick = DetectKnick(sample);
        if (knick != null)
        {
            _findings.Add(knick);
            _lastKnickMeter = knick.MeterPosition;
            _logger?.LogInformation(
                "Knick erkannt: {Angle:F1}° bei {Meter:F2}m (Frame {Frame}, Muffe: {Joint})",
                knick.AngleDeg, knick.MeterPosition, knick.FrameIndex, knick.JointDetected);
        }

        return knick;
    }

    /// <summary>
    /// Prüft ob der aktuelle Frame einen Knick enthaelt.
    /// Vergleicht den aktuellen Fluchtpunkt mit dem geglaetteten Mittelwert
    /// der letzten N Frames.
    /// </summary>
    private KnickFinding? DetectKnick(AxisSample current)
    {
        // Mindestabstand zum letzten Knick (0.3m)
        if (current.Meter - _lastKnickMeter < 0.3)
            return null;

        // Geglaetteter Mittelwert der letzten N Frames (vor dem aktuellen)
        int histCount = _history.Count;
        int windowStart = Math.Max(0, histCount - 1 - SmoothingWindow);
        int windowEnd = histCount - 1; // exklusive aktueller Frame

        if (windowEnd - windowStart < 3)
            return null;

        var window = _history.GetRange(windowStart, windowEnd - windowStart);
        double avgVx = window.Average(s => s.VanishingX);
        double avgVy = window.Average(s => s.VanishingY);
        double avgRx = window.Average(s => s.PipeRadiusX);
        double avgRy = window.Average(s => s.PipeRadiusY);

        // Shift: Abweichung des aktuellen Fluchtpunkts vom Mittelwert
        double dx = current.VanishingX - avgVx;
        double dy = current.VanishingY - avgVy;
        double shiftNorm = Math.Sqrt(dx * dx + dy * dy);

        // Rohrradius als Referenz (Mittelwert X/Y)
        double pipeR = (avgRx + avgRy) / 2.0;
        if (pipeR < 0.05) pipeR = 0.3; // Fallback

        // Winkel aus Shift relativ zum Rohrradius
        // Geometrie: tan(alpha) ≈ shift / (Referenzlaenge)
        // Referenzlaenge ≈ Rohrradius (empirischer Korrekturfaktor)
        double angleDeg = Math.Atan2(shiftNorm, pipeR * 0.8) * 180.0 / Math.PI;

        // Unter Schwelle → kein Knick
        if (angleDeg < MinKnickAngleDeg)
            return null;

        // Bogen-Ausschluss: Pruefen ob der Shift gradulell war (= Bogen, kein Knick)
        // Ein Knick ist abrupt (grosser Sprung in 1-2 Frames)
        // Ein Bogen ist gleichmaessig (kleiner Shift pro Frame ueber viele Frames)
        if (IsGradualCurve(current))
            return null;

        // Richtung des Knicks (Uhrlage-aehnlich: 0°=rechts, 90°=oben)
        double directionDeg = Math.Atan2(-dy, dx) * 180.0 / Math.PI;

        // Konfidenz: hoeher bei Muffe + hohem Fluchtpunkt-Confidence
        double conf = current.Confidence;
        if (current.HasJoint) conf = Math.Min(1.0, conf + 0.2);

        // Muffe in den letzten 3 Frames?
        bool jointNearby = current.HasJoint;
        if (!jointNearby)
        {
            int lookback = Math.Max(0, _history.Count - 4);
            for (int i = lookback; i < _history.Count; i++)
            {
                if (_history[i].HasJoint) { jointNearby = true; break; }
            }
        }

        // Ohne Muffe: Konfidenz senken (Knick nur an Rohrverbindung moeglich)
        if (!jointNearby) conf *= 0.5;

        // Knick nur melden wenn Konfidenz genuegend
        if (conf < 0.35)
            return null;

        return new KnickFinding(
            AngleDeg: Math.Round(angleDeg, 1),
            MeterPosition: current.Meter,
            FrameIndex: current.FrameIndex,
            DirectionDeg: Math.Round(directionDeg, 1),
            Confidence: Math.Round(conf, 2),
            JointDetected: jointNearby
        );
    }

    /// <summary>
    /// Prueft ob die Bewegung graduell ist (= Bogen, kein Knick).
    /// Ein Bogen zeigt gleichmaessigen, kleinen Shift ueber viele Frames.
    /// Ein Knick zeigt einen grossen Sprung in 1-2 Frames.
    /// </summary>
    private bool IsGradualCurve(AxisSample current)
    {
        int count = _history.Count;
        if (count < 6) return false;

        // Letzte 6 Frames: Frame-zu-Frame Shifts berechnen
        var recent = _history.GetRange(Math.Max(0, count - 7), Math.Min(7, count));
        var shifts = new List<double>();

        for (int i = 1; i < recent.Count; i++)
        {
            double dx = recent[i].VanishingX - recent[i - 1].VanishingX;
            double dy = recent[i].VanishingY - recent[i - 1].VanishingY;
            shifts.Add(Math.Sqrt(dx * dx + dy * dy));
        }

        if (shifts.Count < 3) return false;

        // Wenn alle Einzelshifts unter dem Schwellwert liegen → Bogen
        // (gleichmaessige, kleine Verschiebung pro Frame)
        double maxSingleShift = shifts.Max();
        double avgShift = shifts.Average();

        // Bogen: alle Shifts aehnlich klein (Varianz gering)
        // Knick: ein einzelner Shift ist viel groesser als der Durchschnitt
        bool allSmall = maxSingleShift < BogenMaxShiftPerFrame;
        bool evenlyDistributed = shifts.Count > 0 && maxSingleShift < avgShift * 2.5;

        return allSmall && evenlyDistributed;
    }

    /// <summary>Interner Tracking-Datensatz pro Frame.</summary>
    private sealed record AxisSample(
        int FrameIndex,
        double Meter,
        double VanishingX,
        double VanishingY,
        double PipeRadiusX,
        double PipeRadiusY,
        double Confidence,
        bool HasJoint
    );
}
