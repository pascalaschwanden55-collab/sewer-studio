using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Zustand einer Codier-Session: Durchlauf von 0.00m bis Haltungsende.
/// Jede Haltung wird genau einmal komplett durchcodiert.
/// </summary>
public enum CodingSessionState
{
    NotStarted,
    Running,
    Paused,
    WaitingForUserInput,   // KI-Vorschlag wartet auf Bestaetigung
    Completed,
    Aborted
}

/// <summary>
/// Typ des Overlay-Zeichenwerkzeugs.
/// </summary>
public enum OverlayToolType
{
    None = 0,
    Line = 1,            // Linie fuer Risse (Laenge/Breite)
    Arc = 2,             // Bogen fuer Umfangsschaeden (UhrVon/UhrBis)
    Rectangle = 3,       // Rechteck fuer Flaechenschaeden
    Point = 4,           // Punkt fuer Einzelschaeden
    Stretch = 5,         // Strecke fuer Streckenschaeden (MeterStart/MeterEnd)
    PipeBend = 6,        // 4 Punkte: 2× Rohrachse → Biegewinkel
    LateralCircle = 7,   // 3 Punkte am Rand → Umkreis fuer Anschlussdurchmesser
    Ruler = 8,           // Lineal mit Skalenteilung (legacy-kompatibel)
    Protractor = PipeBend,   // Legacy-Alias (Abwaertskompatibilitaet)
    DnCircle = LateralCircle, // Legacy-Alias (Abwaertskompatibilitaet)
    Level = 9,           // Horizontale Linie → Kreissegment-% (Ablagerung/Wasser/Hindernis)
    Ellipse = 10,        // Ellipse/Kreis fuer Flaechenschaeden (Ecke-zu-Ecke Drag)
    Freehand = 11,       // Freihand-Zeichnung (Polyline aus Mauspfad)
    CrossSection = 12    // Querschnittsverminderung (Polygon mit FillPercent)
}

/// <summary>
/// Sub-Modus fuer das Level-Werkzeug.
/// </summary>
public enum LevelMode
{
    Deposit,    // Ablagerung: von Sohle nach oben
    Water,      // Wasser: von Sohle nach oben
    Obstacle    // Hindernis: von Scheitel nach unten
}

/// <summary>
/// Overlay-Geometrie: Ergebnis einer Zeichenoperation auf dem Video-Frame.
/// Value Object — wird zusammen mit ProtocolEntry gespeichert.
/// </summary>
public sealed class OverlayGeometry
{
    public Guid GeometryId { get; set; } = Guid.NewGuid();
    public OverlayToolType ToolType { get; set; }

    // Pixel-Koordinaten auf dem Frame (normiert 0.0–1.0 relativ zur Bildgroesse)
    public List<NormalizedPoint> Points { get; set; } = new();

    // Berechnete Realwelt-Werte (aus Kalibrierung)
    public double? Q1Mm { get; set; }        // Quantifizierung 1 in mm (z.B. Risslaenge)
    public double? Q2Mm { get; set; }        // Quantifizierung 2 in mm (z.B. Rissbreite)
    public double? ClockFrom { get; set; }   // Uhrposition Von (0.0–12.0)
    public double? ClockTo { get; set; }     // Uhrposition Bis (0.0–12.0)
    public double? ArcDegrees { get; set; }  // Bogenwinkel in Grad (PipeBend / Arc)
    public double? DnRatioPercent { get; set; }  // Verhaeltnis zum Haupt-DN in Prozent (LateralCircle)
    public double? FillPercent { get; set; }    // Kreissegment-% fuer Level-Tool (0-100)
    public LevelMode? LevelSubMode { get; set; } // Sub-Modus fuer Level-Tool
    public double? EllipseRadiusXMm { get; set; }  // Horizontaler Radius in mm (Ellipse-Tool)
    public double? EllipseRadiusYMm { get; set; }  // Vertikaler Radius in mm (Ellipse-Tool)

    // Referenz zum Snapshot-Bild (PNG mit Overlay eingebrannt)
    public string? SnapshotPath { get; set; }
}

/// <summary>
/// Normierter Punkt (0.0–1.0) relativ zur Frame-Groesse.
/// Unabhaengig von Aufloesung/Skalierung.
/// </summary>
public sealed class NormalizedPoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public NormalizedPoint() { }
    public NormalizedPoint(double x, double y) { X = x; Y = y; }
}

/// <summary>
/// Ein Codier-Ereignis: Kombination aus ProtocolEntry + optionalem Overlay.
/// </summary>
public sealed class CodingEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Referenz zum ProtocolEntry (wird ins Protokoll uebernommen)
    public ProtocolEntry Entry { get; set; } = new();

    // Optionale Overlay-Geometrie (Zeichnung auf dem Frame)
    public OverlayGeometry? Overlay { get; set; }

    // KI-Vorschlag der zu diesem Event gefuehrt hat (null = rein manuell)
    public CodingEventAiContext? AiContext { get; set; }

    // Meter-Position im Video zum Zeitpunkt der Erfassung
    public double MeterAtCapture { get; set; }

    // Frame-Zeitstempel im Video
    public TimeSpan VideoTimestamp { get; set; }

    /// <summary>Foto-Indicator fuer die Ereignis-Liste (z.B. "📷1" oder "📷2").</summary>
    public string PhotoIndicator =>
        Entry.FotoPaths.Count switch
        {
            0 => "",
            1 => "\U0001F4F7\u0031",  // Kamera + 1
            _ => "\U0001F4F7\u0032"   // Kamera + 2
        };
}

/// <summary>
/// KI-Kontext eines Codier-Events: Was hat die KI vorgeschlagen, was hat der User gemacht.
/// </summary>
public sealed class CodingEventAiContext
{
    public string? SuggestedCode { get; set; }
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public CodingUserDecision Decision { get; set; } = CodingUserDecision.Ignored;
}

public enum CodingUserDecision
{
    Accepted,           // Uebernommen wie vorgeschlagen
    AcceptedWithEdit,   // Uebernommen mit Korrektur
    Rejected,           // Verworfen
    Ignored             // Uebersprungen
}

/// <summary>
/// Kalibrierungsdaten fuer Pixel → Millimeter Umrechnung.
/// Basiert auf bekanntem Rohrdurchmesser (DN).
/// </summary>
public sealed class PipeCalibration
{
    public int NominalDiameterMm { get; set; }     // DN z.B. 300
    public double PipePixelDiameter { get; set; }  // Gemessener Rohrdurchmesser in Canvas-Pixel
    public double NormalizedDiameter { get; set; }  // Rohrdurchmesser als normierter Wert (0.0–1.0)
    public NormalizedPoint PipeCenter { get; set; } = new(0.5, 0.5); // Rohrmitte (normiert)

    /// <summary>Manuell kalibriert (Referenzlinie wurde vom User gezogen)?</summary>
    public bool WasManuallyCalibrated { get; set; }

    /// <summary>Ist kalibriert (Referenzlinie wurde gezeichnet)?</summary>
    public bool IsCalibrated => WasManuallyCalibrated && NormalizedDiameter > 0;

    /// <summary>mm pro normiertem Pixel.</summary>
    public double MmPerNormUnit => NormalizedDiameter > 0
        ? NominalDiameterMm / NormalizedDiameter
        : 0;

    /// <summary>Normierte Laenge in Millimeter umrechnen.</summary>
    public double NormToMm(double normalizedLength)
    {
        if (NormalizedDiameter <= 0) return normalizedLength * 500; // Fallback
        return normalizedLength * MmPerNormUnit;
    }

    /// <summary>Pixel (normiert) in Millimeter umrechnen.</summary>
    public double PixelToMm(double normalizedPixels, double frameWidthPx)
    {
        if (NormalizedDiameter > 0)
            return NormToMm(normalizedPixels);
        if (PipePixelDiameter <= 0) return 0;
        double pipePixelNormalized = PipePixelDiameter / frameWidthPx;
        double mmPerNormPixel = NominalDiameterMm / pipePixelNormalized;
        return normalizedPixels * mmPerNormPixel;
    }

    /// <summary>
    /// Aspect-Ratio-korrigierte Distanz zwischen zwei normierten Punkten.
    /// Normierte Koordinaten: X=0..1 ueber Bildbreite, Y=0..1 ueber Bildhoehe.
    /// Bei nicht-quadratischen Bildern muss X mit Aspect (W/H) skaliert werden.
    /// </summary>
    /// <param name="a">Startpunkt (normiert).</param>
    /// <param name="b">Endpunkt (normiert).</param>
    /// <param name="imageAspect">Seitenverhaeltnis (Breite/Hoehe). 1.0 fuer quadratisch, 1.78 fuer 16:9.</param>
    public static double AspectCorrectedDistance(NormalizedPoint a, NormalizedPoint b, double imageAspect = 1.0)
    {
        double dx = (b.X - a.X) * imageAspect;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Aspect-korrigierte normierte Laenge in mm umrechnen.</summary>
    public double NormToMmAspect(NormalizedPoint a, NormalizedPoint b, double imageAspect = 1.0)
    {
        double dist = AspectCorrectedDistance(a, b, imageAspect);
        return NormToMm(dist);
    }

    /// <summary>Punkt auf dem Frame → Uhrposition (0.0–12.0).</summary>
    public double PointToClockHour(NormalizedPoint point)
    {
        double dx = point.X - PipeCenter.X;
        double dy = point.Y - PipeCenter.Y;
        // atan2: 0° = rechts, wir wollen 0° = oben (12 Uhr)
        double angleRad = Math.Atan2(dx, -dy); // -dy weil Y nach unten waechst
        double angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360;
        return angleDeg / 30.0; // 360° / 12 Stunden = 30° pro Stunde
    }
}

/// <summary>
/// Ergebnis des PhotoMeasurementWindow (PhotoAssistant).
/// Wird vom VsaCodeExplorerWindow ausgewertet.
/// </summary>
public sealed class PhotoMeasurementResult
{
    public OverlayGeometry? Geometry { get; set; }
    public string? OverlayPhotoPath { get; set; }
    public bool Confirmed { get; set; }
    public PipeCalibration? UpdatedCalibration { get; set; }
}

/// <summary>
/// Codier-Session: Aggregate Root fuer den Durchlauf einer Haltung.
/// </summary>
public sealed class CodingSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid HaltungId { get; set; }
    public string HaltungName { get; set; } = "";

    // Meter-Bereich
    public double StartMeter { get; set; }  // Immer 0.00
    public double EndMeter { get; set; }    // Haltungslaenge z.B. 15.23

    // Aktueller Fortschritt
    public double CurrentMeter { get; set; }
    public CodingSessionState State { get; set; } = CodingSessionState.NotStarted;

    // Video-Referenz
    public string? VideoPath { get; set; }

    // Kalibrierung (optional, fuer Overlay-Messungen)
    public PipeCalibration? Calibration { get; set; }

    // Gesammelte Ereignisse (chronologisch nach Meter)
    public List<CodingEvent> Events { get; set; } = new();

    // Session-Metadata
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? AbortReason { get; set; }

    // Fortschritt in Prozent
    public double ProgressPercent =>
        EndMeter > StartMeter
            ? Math.Clamp((CurrentMeter - StartMeter) / (EndMeter - StartMeter) * 100.0, 0, 100)
            : 0;
}
