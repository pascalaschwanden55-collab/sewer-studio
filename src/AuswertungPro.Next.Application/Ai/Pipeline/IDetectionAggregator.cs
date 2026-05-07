using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Audit 2026-04-23 ARCH-H3: Interface fuer den temporal-Voting-DetectionAggregator.
/// Erlaubt Mock-basierte Unit-Tests fuer Pipeline-Konsumenten ohne den realen
/// Aggregations-State (active Tracks, Gap-Detection) mit-aufzubauen.
///
/// Implementierung: <see cref="DetectionAggregator"/>.
/// </summary>
public interface IDetectionAggregator
{
    /// <summary>
    /// Eine einzelne Frame-Detektion einspeisen. Gibt ein geschlossenes
    /// Event zurueck wenn das vorherige durch einen Gap finalisiert wurde,
    /// sonst null.
    /// </summary>
    DetectionEvent? Feed(FrameDetection detection);

    /// <summary>
    /// Schliesst alle noch aktiven Detektionen am Video-Ende. Gibt die
    /// resultierenden Events zurueck (gefiltert nach minConsecutiveFrames).
    /// </summary>
    List<DetectionEvent> Flush();
}
