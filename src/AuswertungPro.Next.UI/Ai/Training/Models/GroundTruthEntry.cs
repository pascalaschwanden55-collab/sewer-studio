// AuswertungPro – KI Videoanalyse Modul
namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>
/// Eine aus einem PDF-Protokoll extrahierte Schadensbeobachtung (Ground-Truth).
/// Dient als Referenz für Sample-Generierung und KI-Evaluation.
/// </summary>
public sealed record GroundTruthEntry
{
    /// <summary>Meter-Position Beginn (aus Protokoll).</summary>
    public required double MeterStart { get; init; }

    /// <summary>Meter-Position Ende. Gleich MeterStart bei Einzelschaden.</summary>
    public required double MeterEnd { get; init; }

    /// <summary>VSA-Code (z.B. "BAB", "BCD").</summary>
    public required string VsaCode { get; init; }

    /// <summary>Protokolltext wie im PDF angegeben.</summary>
    public required string Text { get; init; }

    /// <summary>Strukturierte Quantifizierung (Wert, Einheit, Typ). Null wenn nicht vorhanden.</summary>
    public QuantificationDetail? Quantification { get; init; }

    /// <summary>Charakterisierung A–D (falls der Code es erfordert).</summary>
    public string? Characterization { get; init; }

    /// <summary>Uhrzeigerposition als String (z.B. "3" = 3 Uhr). Null wenn nicht angegeben.</summary>
    public string? ClockPosition { get; init; }

    /// <summary>Uhrzeigerposition Anschluss (bei Anschlussschäden).</summary>
    public string? ConnectionClock { get; init; }

    /// <summary>True wenn der Schaden eine Strecke (MeterStart bis MeterEnd) betrifft.</summary>
    public bool IsStreckenschaden { get; init; }

    /// <summary>
    /// Exakter Video-Zeitstempel aus dem PDF-Protokoll (z.B. HH:MM:SS).
    /// Null wenn kein Timestamp im Protokoll vorhanden.
    /// Wird bevorzugt gegenüber linearer Zeitinterpolation.
    /// </summary>
    public TimeSpan? Zeit { get; init; }
}
