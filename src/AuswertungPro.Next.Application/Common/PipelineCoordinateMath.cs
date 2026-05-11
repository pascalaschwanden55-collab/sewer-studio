using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Koordinaten-Mathematik fuer den Pipeline-Pfad: Letterbox/Pillarbox-
/// Korrektur, normalisierte Pixel-Distanz im Source-Frame.
///
/// Cherry-Pick aus archive/2026-05-10-robustifizierungen (Deep-Dive #6).
/// Vorher: Canvas-Pixel-Distanz wurde an SAM gegeben — bei Letterbox
/// landete die Maske an verschobener Stelle. Jetzt: Pure-Functions die
/// Norm-Coords sauber in den Source-Frame-Pixel-Raum mappen.
///
/// Pure-Functions, Domain-Modell `NormalizedPoint` als Eingabe — keine
/// UI-Abhaengigkeit, klassisch testbar.
/// </summary>
public static class PipelineCoordinateMath
{
    /// <summary>
    /// Pixel-Distanz zwischen zwei NormalizedPoints, gerechnet im
    /// Source-Frame-Koordinatensystem (nicht Canvas). Verwendet fuer
    /// die manuelle Kalibrierung — vorher wurde Canvas-Pixel-Distanz
    /// verwendet, was bei Letterbox/Pillarbox eine um den Stretch-Faktor
    /// verzerrte Pipe-Diameter-Pixel-Angabe lieferte.
    /// </summary>
    /// <returns>Pixel-Distanz im Source-Frame, oder 0 wenn die uebergebene
    /// Frame-Aufloesung ungueltig ist (Caller muss Fallback handeln).</returns>
    public static double ComputeSourceFramePixelDiameter(
        NormalizedPoint start, NormalizedPoint end,
        int sourceFrameWidth, int sourceFrameHeight)
    {
        if (sourceFrameWidth <= 0 || sourceFrameHeight <= 0)
            return 0.0;
        var dx = (end.X - start.X) * sourceFrameWidth;
        var dy = (end.Y - start.Y) * sourceFrameHeight;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Sichtbarer Video-Bereich innerhalb einer Container-Flaeche nach
    /// Stretch=Uniform-Layout. Liefert Position + Groesse des Frames
    /// (im Container-Pixel-Raum) — Caller kann damit Canvas-Coords auf
    /// Frame-Coords mappen.
    ///
    /// Bei Pillarbox (Container breiter als Video-AR): Balken links/rechts.
    /// Bei Letterbox (Container hoeher): Balken oben/unten.
    /// Bei ungueltigen Eingaben: gibt volles Container-Rect zurueck (Fallback).
    /// </summary>
    public static VideoContentRect ComputeVideoContentRect(
        double containerWidth, double containerHeight,
        int sourceFrameWidth, int sourceFrameHeight)
    {
        if (containerWidth <= 0 || containerHeight <= 0)
            return new VideoContentRect(0, 0, 0, 0);

        if (sourceFrameWidth <= 0 || sourceFrameHeight <= 0)
            return new VideoContentRect(0, 0, containerWidth, containerHeight);

        double videoAR = sourceFrameWidth / (double)sourceFrameHeight;
        double containerAR = containerWidth / containerHeight;

        if (containerAR > videoAR)
        {
            // Pillarbox: Balken links/rechts.
            double contentW = containerHeight * videoAR;
            double leftBar = (containerWidth - contentW) / 2.0;
            return new VideoContentRect(leftBar, 0, contentW, containerHeight);
        }
        else
        {
            // Letterbox: Balken oben/unten.
            double contentH = containerWidth / videoAR;
            double topBar = (containerHeight - contentH) / 2.0;
            return new VideoContentRect(0, topBar, containerWidth, contentH);
        }
    }

    /// <summary>
    /// Canvas-Pixel → NormalizedPoint im Source-Frame-Koordinatensystem.
    /// Berueckt die Letterbox/Pillarbox-Balken durch ComputeVideoContentRect.
    /// Werte werden auf [0,1] geclamped — Klicks ausserhalb des Video-Bereichs
    /// landen am naechstgelegenen Rand.
    /// </summary>
    public static NormalizedPoint CanvasPixelToNormalized(
        double canvasX, double canvasY,
        double containerWidth, double containerHeight,
        int sourceFrameWidth, int sourceFrameHeight)
    {
        var content = ComputeVideoContentRect(
            containerWidth, containerHeight,
            sourceFrameWidth, sourceFrameHeight);

        if (content.Width <= 0 || content.Height <= 0)
            return new NormalizedPoint(0.5, 0.5);

        double nx = (canvasX - content.X) / content.Width;
        double ny = (canvasY - content.Y) / content.Height;
        return new NormalizedPoint(
            Math.Clamp(nx, 0.0, 1.0),
            Math.Clamp(ny, 0.0, 1.0));
    }
}

/// <summary>Sichtbarer Video-Bereich innerhalb eines Containers (in
/// Container-Pixeln). X/Y = Start des Bereichs (Pillarbox-/Letterbox-Offset),
/// Width/Height = Groesse des Video-Inhalts ohne Balken.</summary>
public readonly record struct VideoContentRect(
    double X, double Y, double Width, double Height);
