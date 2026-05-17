using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Vision;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Konsens- und Qualitaetsfilter fuer Multi-Model-Detektionen.
///
/// Aus <c>MultiModelAnalysisService.Helpers.cs</c> migriert (Audit 2026-05-13 M1).
/// Pure-Logic-Klasse: keine HTTP-, DB- oder UI-Abhaengigkeiten.
/// </summary>
public static class ConsensusQualityFilter
{
    /// <summary>YOLO-Mindestkonfidenz fuer Konsens-Beitrag.</summary>
    public const double YoloMinConf = 0.20;

    /// <summary>DINO-Mindestkonfidenz fuer Konsens-Beitrag.</summary>
    public const double DinoMinConf = 0.25;

    /// <summary>Qwen-Mindestkonfidenz fuer Konsens-Beitrag (vorher 0.40 — zu nah an Zufall).</summary>
    public const double QwenMinConf = 0.55;

    /// <summary>
    /// Minimale BBox-Flaeche (normiert): Objekte unter ~3% der Bildflaeche sind zu weit weg
    /// (max ~20cm Entfernung bei typischen Kanalrohren DN100-DN600).
    /// </summary>
    public const double MinBboxArea = 0.03;

    /// <summary>
    /// Filtert Detektionen die nicht von mindestens 2 Modellen bestaetigt werden,
    /// entfernt Red-QualityGate-Ergebnisse und verwirft zu kleine/weit entfernte Objekte.
    /// </summary>
    /// <param name="detections">Liste wird in-place gefiltert.</param>
    /// <param name="logger">Optional fuer Protokollierung des Filter-Resultats.</param>
    /// <param name="qualityGate">Optional injizierbarer QualityGate-Service (Tests). Default: neue <see cref="QualityGateService"/>-Instanz.</param>
    /// <returns>Anzahl entfernter Detektionen.</returns>
    public static int Apply(
        List<RawVideoDetection> detections,
        ILogger? logger = null,
        IQualityGateService? qualityGate = null)
    {
        if (detections == null || detections.Count == 0) return 0;

        var qg = qualityGate ?? new QualityGateService();
        var before = detections.Count;

        detections.RemoveAll(d =>
        {
            // 0. Zu kleine/weit entfernte Objekte verwerfen
            if (d.BboxX1 is not null && d.BboxY1 is not null &&
                d.BboxX2 is not null && d.BboxY2 is not null)
            {
                var bboxW = Math.Abs(d.BboxX2.Value - d.BboxX1.Value);
                var bboxH = Math.Abs(d.BboxY2.Value - d.BboxY1.Value);
                var area = bboxW * bboxH;
                if (area < MinBboxArea)
                    return true;
            }

            if (d.Evidence is not { } ev) return false; // Kein Evidence → behalten (Legacy)

            // 1. QualityGate Red → raus
            var result = qg.Evaluate(ev);
            if (result.IsRed)
                return true;

            // 2. Multi-Model-Konsens: mindestens 2 Modelle muessen bestaetigen
            int confirmations = 0;
            if (ev.YoloConf is >= YoloMinConf) confirmations++;
            if (ev.DinoConf is >= DinoMinConf) confirmations++;
            if (ev.QwenVisionConf is >= QwenMinConf) confirmations++;

            return confirmations < 2;
        });

        var removed = before - detections.Count;
        if (removed > 0)
        {
            logger?.LogInformation(
                "Konsens+QualityGate-Filter: {Before} → {After} Detektionen ({Removed} entfernt)",
                before, detections.Count, removed);
        }
        return removed;
    }
}
