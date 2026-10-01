using System;
using System.Diagnostics;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Werte genau eines Bildes, die alle Modellschritte der Mehrmodell-Analyse gemeinsam brauchen
/// (AP05b): Bilddaten, Trace, Zeitmessung und der geschaetzte Meter vor der Inferenz. Die
/// Modellzeiten von YOLO und DINO traegt der jeweilige Schritt nach, damit die folgenden
/// Schritte ihre Telemetrie mit denselben Werten schreiben wie vor der Auslagerung.
/// Enthaelt keine Entscheidungen, nur Werte; lebt genau ein Bild.
/// </summary>
internal sealed class MultiModelBildKontext
{
    public MultiModelBildKontext(
        double t, byte[] frameBytes, PipelineFrameTrace trace, Stopwatch frameSw, long extractionMs, double estimatedMeter)
    {
        T = t;
        FrameBytes = frameBytes;
        FrameBase64 = Convert.ToBase64String(frameBytes);
        Trace = trace;
        FrameSw = frameSw;
        ExtractionMs = extractionMs;
        EstimatedMeter = estimatedMeter;
    }

    /// <summary>Zeitpunkt des Bildes im Video (Sekunden).</summary>
    public double T { get; }

    public byte[] FrameBytes { get; }
    public string FrameBase64 { get; }
    public PipelineFrameTrace Trace { get; }

    /// <summary>Laeuft seit Beginn des Bildes; Gesamtzeit der Telemetrie.</summary>
    public Stopwatch FrameSw { get; }

    public long ExtractionMs { get; }

    /// <summary>Linear geschaetzter Meter zu Beginn des Bildes (Meter aller Fehler- und Skip-Ausgaenge).</summary>
    public double EstimatedMeter { get; }

    /// <summary>YOLO-Dauer (0 bei Umgehung); gesetzt vom YOLO-Schritt.</summary>
    public long YoloMs { get; set; }

    /// <summary>DINO-Dauer; gesetzt vom DINO-Schritt.</summary>
    public long DinoMs { get; set; }

    /// <summary>
    /// Telemetrie eines Bildes ohne Qwen-Anteil. Die Gesamtzeit wird erst hier gelesen, also nach
    /// den vom Aufrufer uebergebenen Phasenzeiten - dieselbe Reihenfolge wie vorher inline.
    /// </summary>
    public void RecordFrame(MultiModelLaufZustand run, long yoloMs, long dinoMs, long samMs, bool skipped = true)
        => run.Telemetry.RecordFrame(new FrameTiming(run.FrameIndex, T, ExtractionMs, yoloMs, dinoMs, samMs, 0,
            FrameSw.ElapsedMilliseconds, Skipped: skipped));
}
