using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Overlay-Zeichenwerkzeuge fuer Video-Frames.
/// Wandelt Pixel-Zeichnungen in VSA-Parameter (Q1, Q2, Uhr) um.
/// Snapshot-Export ist WPF-spezifisch und wird in der Implementierung gehandhabt.
/// </summary>
public interface IOverlayToolService
{
    // --- Werkzeug-Auswahl ---

    /// <summary>Aktives Zeichenwerkzeug.</summary>
    OverlayToolType ActiveTool { get; set; }

    /// <summary>Werkzeug gewechselt.</summary>
    event EventHandler<OverlayToolType>? ToolChanged;

    // --- Kalibrierung ---

    /// <summary>Kalibrierung setzen (DN + Rohr-Pixel-Durchmesser).</summary>
    void SetCalibration(PipeCalibration calibration);

    /// <summary>Aktive Kalibrierung.</summary>
    PipeCalibration? Calibration { get; }

    /// <summary>Ist kalibriert?</summary>
    bool IsCalibrated { get; }

    // --- Zeichenoperationen (normierte Koordinaten 0.0–1.0) ---

    /// <summary>Zeichnung starten (MouseDown).</summary>
    void BeginDraw(NormalizedPoint startPoint);

    /// <summary>Zeichnung aktualisieren (MouseMove).</summary>
    void UpdateDraw(NormalizedPoint currentPoint);

    /// <summary>Zeichnung abschliessen (MouseUp) → OverlayGeometry zurueck.</summary>
    OverlayGeometry? EndDraw();

    /// <summary>Aktuelle Zeichnung abbrechen.</summary>
    void CancelDraw();

    /// <summary>Wird gerade gezeichnet?</summary>
    bool IsDrawing { get; }

    // --- Multi-Punkt-Werkzeuge (z.B. Winkelmesser mit 3 Klicks) ---

    /// <summary>Ist das aktive Werkzeug ein Multi-Punkt-Werkzeug?</summary>
    bool IsMultiPointTool { get; }

    /// <summary>Anzahl benoetigter Punkte fuer das aktive Multi-Punkt-Werkzeug.</summary>
    int RequiredPointCount { get; }

    /// <summary>Anzahl bereits gesetzter Punkte.</summary>
    int DrawPointCount { get; }

    /// <summary>Punkt hinzufuegen (Multi-Punkt-Werkzeug). Gibt true zurueck wenn genug Punkte gesammelt.</summary>
    bool AddDrawPoint(NormalizedPoint point);

    /// <summary>Alle bisher gesetzten Multi-Punkt-Punkte (fuer Vorschau-Rendering).</summary>
    IReadOnlyList<NormalizedPoint> DrawPoints { get; }

    // --- Berechnungen ---

    /// <summary>Pixel (normiert) in Millimeter umrechnen.</summary>
    double PixelToMm(double normalizedPixels, double frameWidthPx);

    /// <summary>Punkt → Uhrposition (0.0–12.0).</summary>
    double PointToClockHour(NormalizedPoint point);

    // --- Vorschau (fuer UI-Rendering waehrend des Zeichnens) ---

    /// <summary>Aktueller Start-Punkt der laufenden Zeichnung.</summary>
    NormalizedPoint? DrawStartPoint { get; }

    /// <summary>Aktueller End-Punkt der laufenden Zeichnung.</summary>
    NormalizedPoint? DrawCurrentPoint { get; }

    /// <summary>Vorschau-Geometrie waehrend des Zeichnens (fuer Canvas-Rendering).</summary>
    OverlayGeometry? PreviewGeometry { get; }
}
