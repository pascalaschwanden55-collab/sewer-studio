namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis-Stufe der Naehe-Pruefung eines KI-Befunds.</summary>
public enum MetrierungProximity
{
    /// <summary>Nah genug: darf metriert und codiert werden.</summary>
    Codierbar,
    /// <summary>Noch zu weit voraus: anzeigen, aber nicht metrieren/codieren.</summary>
    Voraus
}

/// <summary>
/// Reine Eingabe der Naehe-Pruefung. Alle Koordinaten normiert (0..1).
/// Entkoppelt die Logik von Infrastructure-DTOs.
/// </summary>
public sealed record MetrierungProximityInput(
    double X1, double Y1, double X2, double Y2,   // Befund-Box, normiert
    double VanishX, double VanishY,                // Fluchtpunkt (Rohrmitte), normiert
    double ImageAspect,                            // Bildbreite / Bildhoehe (>= 1 bei Querformat)
    double PipeRadiusNorm,                         // Rohrradius normiert (NormalizedDiameter/2; Fallback 0.5)
    bool IsDirectionalEvent = false);              // Bogen (BCC): zentral/verschobener Fluchtpunkt, kein Wand-Punktschaden

/// <summary>Ergebnis der Naehe-Pruefung mit Begruendung und Messwerten (fuer Tests/Diagnose).</summary>
public sealed record MetrierungProximityResult(
    MetrierungProximity Decision,
    string Reason,
    double FillRatio,
    double DistToVanish,
    double OuterRadius,
    bool WandNaehe,
    bool EnthaeltCenter)
{
    public bool IsCodierbar => Decision == MetrierungProximity.Codierbar;
}

/// <summary>
/// Kalibrierbare Schwellen. Bewusst konservativ: im Zweifel "Voraus".
/// Alle Distanz-Schwellen sind in Einheiten des Rohrradius (1.0 = an der Rohrwand).
/// </summary>
public sealed record MetrierungProximityThresholds(
    double FillNear = 0.70,       // Boxhoehe/Bildhoehe ab der ein Ereignis "querschnittsfuellend nah" ist
    double CenterNear = 0.20,     // Box-Zentrum naeher als das am Fluchtpunkt -> zentral
    double RadialOutside = 0.45,  // Box-Zentrum weiter als das -> klar aussen an der Wand
    double WallTolerance = 0.12)  // Toleranz fuer Wand-/Bildrand-Kontakt
{
    public static MetrierungProximityThresholds Default { get; } = new();
}
