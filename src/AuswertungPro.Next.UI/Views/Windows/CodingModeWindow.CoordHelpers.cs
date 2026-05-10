using System;
using System.Windows;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Pixel/Norm-Umrechnungshelfer (Slice 8a.2.7): zwei
// reine Koordinaten-Konverter (Pixel ↔ NormalizedPoint anhand der
// OverlayCanvas-Groesse). Keine State- oder Workflow-Aenderung.
//
// Letterbox/Pillarbox-Fix (2026-05-10): Bei Stretch=Uniform laesst die VLC-
// VideoView schwarze Balken am Rand. OverlayCanvas fuellt den ganzen Container,
// ist also groesser als der sichtbare Frame. Vorher rechneten wir
// Canvas-Pixel → Norm direkt (Canvas-Groesse als Referenz) → BBox-Coords
// landeten beim SAM/Qwen-Aufruf an der falschen Source-Frame-Stelle.
// Jetzt: ComputeVideoContentRect liefert den sichtbaren Video-Bereich
// (innerhalb des Canvas), Norm-Coords werden relativ dazu gerechnet.
//
// Fallback: wenn _player.Width/Height noch nicht bekannt (Video nicht ready),
// fallen wir auf das alte Verhalten zurueck (komplette Canvas-Flaeche). Dann
// kann der User notfalls schon zeichnen, auch wenn die Source-Frame-Mapping
// noch ungenau ist — Roundtrip BBox-Cursor-Anzeige stimmt trotzdem.
public partial class CodingModeWindow
{
    /// <summary>Berechnet den sichtbaren Video-Bereich innerhalb der OverlayCanvas
    /// nach dem Stretch=Uniform-Layout der VideoView. Liefert das volle Canvas-
    /// Rect, wenn Player-Dimensionen unbekannt sind (Video nicht bereit) oder
    /// kein gueltiges Aspect-Ratio ableitbar ist.</summary>
    private Rect ComputeVideoContentRect()
    {
        double canvasW = OverlayCanvas.ActualWidth;
        double canvasH = OverlayCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0)
            return new Rect(0, 0, 0, 0);

        // Frame-Dimensionen werden vom ersten erfolgreichen Capture gecacht
        // (siehe CodingModeWindow.FrameCapture / OnPlayerFirstPlaying-Pfad).
        // Solange Cache leer: Fallback auf volles Canvas — Roundtrip
        // BBox-Cursor stimmt, nur SAM-Source-Frame-Mapping ist ungenau bis
        // erstes Capture lief.
        if (_videoFrameWidthCache <= 0 || _videoFrameHeightCache <= 0)
            return new Rect(0, 0, canvasW, canvasH);

        double videoAR = _videoFrameWidthCache / (double)_videoFrameHeightCache;
        double canvasAR = canvasW / canvasH;

        if (canvasAR > videoAR)
        {
            // Pillarbox: Balken links/rechts.
            double contentW = canvasH * videoAR;
            double leftBar = (canvasW - contentW) / 2.0;
            return new Rect(leftBar, 0, contentW, canvasH);
        }
        else
        {
            // Letterbox: Balken oben/unten.
            double contentH = canvasW / videoAR;
            double topBar = (canvasH - contentH) / 2.0;
            return new Rect(0, topBar, canvasW, contentH);
        }
    }

    private NormalizedPoint PixelToNormalized(Point pixel)
    {
        var content = ComputeVideoContentRect();
        if (content.Width <= 0 || content.Height <= 0)
            return new NormalizedPoint(0.5, 0.5);

        // Wenn Klick ausserhalb des Video-Rects (auf den schwarzen Balken),
        // clampen wir auf [0,1]. Das ist konservativ — der User kann
        // theoretisch ueber den Rand zeichnen, dann landet die BBox am
        // Bildrand, nicht im Pillarbox-Bereich.
        double nx = (pixel.X - content.X) / content.Width;
        double ny = (pixel.Y - content.Y) / content.Height;
        return new NormalizedPoint(
            Math.Clamp(nx, 0.0, 1.0),
            Math.Clamp(ny, 0.0, 1.0));
    }

    private Point NormalizedToPixel(NormalizedPoint normalized)
    {
        var content = ComputeVideoContentRect();
        return new Point(
            content.X + normalized.X * content.Width,
            content.Y + normalized.Y * content.Height);
    }

    /// <summary>Pixel-Distanz zwischen zwei NormalizedPoints, gerechnet
    /// im Source-Frame-Koordinatensystem (nicht Canvas). Verwendet fuer
    /// die manuelle Kalibrierung (Slice 8a.6.C 2026-05-10) — vorher wurde
    /// Canvas-Pixel-Distanz verwendet, was bei Letterbox/Pillarbox eine
    /// um den Stretch-Faktor verzerrte Pipe-Diameter-Pixel-Angabe lieferte.
    ///
    /// Statisch, deshalb klassisch testbar.</summary>
    /// <returns>Pixel-Distanz im Source-Frame, oder 0 wenn die uebergebene
    /// Frame-Aufloesung ungueltig ist (Caller muss Fallback handeln).</returns>
    internal static double ComputeSourceFramePixelDiameter(
        NormalizedPoint start, NormalizedPoint end,
        int sourceFrameWidth, int sourceFrameHeight)
    {
        if (sourceFrameWidth <= 0 || sourceFrameHeight <= 0)
            return 0.0;
        var dx = (end.X - start.X) * sourceFrameWidth;
        var dy = (end.Y - start.Y) * sourceFrameHeight;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
