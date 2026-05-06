using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Zentrale Bildmanipulations- und Overlay-Hilfsfunktionen, abgeleitet vom WinCan
/// PhotoAssistant Funktionsumfang. Alle Methoden sind reine Bildoperationen ohne
/// UI-Abhaengigkeit (Tests sind direkt auf BitmapSource moeglich).
///
/// WinCan-Werkzeug -> Methode hier:
///   btnImgPan        -> PanImage(...)
///   btnImgRot        -> RotateImage(...)
///   btnImgScale      -> ScaleImage(...)
///   btnImgUndistort  -> UndistortBarrel(...)
///   btnSaveScreen    -> CaptureVisualToPng(...)
///   btnScreenShot    -> RenderWithOverlay(...)
///   btnViewClockH    -> BuildClockHourLines(...)
///   btnViewType0/1/3 -> Build3DPipeOverlay/Build3DBranchOverlay/Build3DJointOffset
/// </summary>
public static class PhotoAssistantTools
{
    // ── Bildmanipulation ─────────────────────────────────────────────────

    /// <summary>
    /// Verschiebt ein Bild um (dx, dy) Pixel auf einer transparent ueberlagerten Leinwand.
    /// Negative Werte = links/oben. Liefert das verschobene Bild als BitmapSource.
    /// </summary>
    public static BitmapSource PanImage(BitmapSource source, double dxPixels, double dyPixels)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var transform = new TranslateTransform(dxPixels, dyPixels);
        return ApplyTransform(source, transform);
    }

    /// <summary>
    /// Rotiert ein Bild um den Mittelpunkt um angleDegrees Grad.
    /// Positiv = im Uhrzeigersinn.
    /// </summary>
    public static BitmapSource RotateImage(BitmapSource source, double angleDegrees)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var rotation = new RotateTransform(angleDegrees, source.PixelWidth / 2.0, source.PixelHeight / 2.0);
        return ApplyTransform(source, rotation);
    }

    /// <summary>
    /// Skaliert ein Bild um faktorX/faktorY (1.0 = 100%). Bilineare Interpolation
    /// erfolgt automatisch ueber WPF.
    /// </summary>
    public static BitmapSource ScaleImage(BitmapSource source, double factorX, double factorY)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (factorX <= 0 || factorY <= 0)
            throw new ArgumentException("Skalierungs-Faktor muss > 0 sein.");
        var scale = new ScaleTransform(factorX, factorY);
        return ApplyTransform(source, scale);
    }

    /// <summary>
    /// Entzerrt eine Tonnen-/Kissen-Verzeichnung mit einfachem Brown-Modell:
    ///   r' = r * (1 + k1*r^2 + k2*r^4)
    /// k1 < 0 = Tonnen-Korrektur, k1 > 0 = Kissen-Korrektur.
    /// k2 verfeinert. Praktische Werte: k1 = -0.3 bis +0.3.
    ///
    /// HINWEIS: einfache CPU-Implementierung, fuer 1280x720 ~50-150ms.
    /// Fuer Echtzeit-Video sollte ein GPU-Shader verwendet werden.
    /// </summary>
    public static BitmapSource UndistortBarrel(BitmapSource source, double k1, double k2 = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var w = source.PixelWidth;
        var h = source.PixelHeight;
        var stride = w * 4;
        var src = new byte[h * stride];
        var src32 = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        src32.CopyPixels(src, stride, 0);

        var dst = new byte[h * stride];
        var cx = w / 2.0;
        var cy = h / 2.0;
        var maxR = Math.Sqrt(cx * cx + cy * cy);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var nx = (x - cx) / maxR;
                var ny = (y - cy) / maxR;
                var r2 = nx * nx + ny * ny;
                var factor = 1 + k1 * r2 + k2 * r2 * r2;
                var srcX = (int)Math.Round(cx + (x - cx) * factor);
                var srcY = (int)Math.Round(cy + (y - cy) * factor);
                var dstIdx = y * stride + x * 4;

                if (srcX >= 0 && srcX < w && srcY >= 0 && srcY < h)
                {
                    var srcIdx = srcY * stride + srcX * 4;
                    dst[dstIdx]     = src[srcIdx];
                    dst[dstIdx + 1] = src[srcIdx + 1];
                    dst[dstIdx + 2] = src[srcIdx + 2];
                    dst[dstIdx + 3] = src[srcIdx + 3];
                }
                // sonst transparent (Default 0)
            }
        }

        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, dst, stride);
        result.Freeze();
        return result;
    }

    private static BitmapSource ApplyTransform(BitmapSource source, Transform transform)
    {
        var bmp = new TransformedBitmap(source, transform);
        bmp.Freeze();
        return bmp;
    }

    // ── Screenshots ──────────────────────────────────────────────────────

    /// <summary>
    /// Rendert ein WPF-Visual (z.B. Image + Overlay-Layer) inklusive aller
    /// Annotationen in eine PNG-Datei. Entspricht btnScreenShot/btnSaveScreen.
    /// </summary>
    public static void CaptureVisualToPng(System.Windows.Media.Visual visual, double width, double height, string outPath)
    {
        if (visual is null) throw new ArgumentNullException(nameof(visual));
        if (string.IsNullOrWhiteSpace(outPath)) throw new ArgumentException("outPath leer");
        var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
        using var fs = File.Create(outPath);
        encoder.Save(fs);
    }

    // ── Overlay-Geometrien ───────────────────────────────────────────────

    /// <summary>
    /// Liefert die 12 Stundenlinien-Endpunkte als (Innen, Aussen) Punkt-Paare.
    /// Eingabe: Mittelpunkt + Radius in normalisierten 0..1 Koordinaten.
    /// Index 0 = 12 Uhr (oben), 3 = 3 Uhr (rechts), 6 = 6 Uhr (unten), 9 = 9 Uhr.
    ///
    /// innerRatio steuert den Beginn der Linien (0.0 = vom Zentrum, 0.85 = nur kurze Striche am Rand).
    /// </summary>
    public static IReadOnlyList<(Point Inner, Point Outer, int Hour)> BuildClockHourLines(
        Point center, double radius, double innerRatio = 0.85)
    {
        if (radius <= 0) throw new ArgumentException("radius muss > 0 sein.");
        if (innerRatio < 0 || innerRatio > 1) throw new ArgumentException("innerRatio in [0..1].");

        var lines = new List<(Point, Point, int)>(12);
        for (var hour = 0; hour < 12; hour++)
        {
            // 12 Uhr = oben = -90° in WPF-Koordinaten (y-Achse zeigt nach unten)
            var angleRad = (hour * 30.0 - 90.0) * Math.PI / 180.0;
            var dx = Math.Cos(angleRad);
            var dy = Math.Sin(angleRad);
            var inner = new Point(center.X + dx * radius * innerRatio, center.Y + dy * radius * innerRatio);
            var outer = new Point(center.X + dx * radius, center.Y + dy * radius);
            // Hour-Index nach Uhrlage-Konvention (12, 1, 2, ... 11)
            var displayHour = hour == 0 ? 12 : hour;
            lines.Add((inner, outer, displayHour));
        }
        return lines;
    }

    /// <summary>
    /// 3D-Einblendung "Rohr" (btnViewType0) - Pseudo-3D-Achse.
    /// Liefert eine Polyline mit 8 Punkten (4 Ellipsen-Vorder/Hinterseite, 4 Mantellinien).
    /// Pseudo-3D = perspektivisch geschrumpfte hintere Ellipse.
    /// </summary>
    public static IReadOnlyList<Point> Build3DPipeOverlay(Point centerFront, double radiusFront,
        Point centerBack, double radiusBack, int segments = 24)
    {
        if (segments < 8) segments = 8;
        var pts = new List<Point>(segments * 2 + 2);
        for (var i = 0; i < segments; i++)
        {
            var t = i / (double)segments * 2 * Math.PI;
            pts.Add(new Point(centerFront.X + Math.Cos(t) * radiusFront,
                              centerFront.Y + Math.Sin(t) * radiusFront));
        }
        for (var i = 0; i < segments; i++)
        {
            var t = i / (double)segments * 2 * Math.PI;
            pts.Add(new Point(centerBack.X + Math.Cos(t) * radiusBack,
                              centerBack.Y + Math.Sin(t) * radiusBack));
        }
        return pts;
    }

    /// <summary>
    /// 3D-Einblendung "Abzweiger" (btnViewType1) - Hauptrohr + zylindrischer Anschluss.
    /// Liefert (HauptrohrPolyline, AnschlussPolyline). Anschluss ist eine Ellipse am Andockpunkt.
    /// </summary>
    public static (IReadOnlyList<Point> Pipe, IReadOnlyList<Point> Branch) Build3DBranchOverlay(
        Point pipeCenter, double pipeRadius,
        Point branchCenter, double branchRadius, int segments = 16)
    {
        var pipe = Build3DPipeOverlay(
            pipeCenter,
            pipeRadius,
            new Point(pipeCenter.X, pipeCenter.Y + pipeRadius * 1.5),
            pipeRadius * 0.85,
            segments);

        var branch = new List<Point>(segments);
        for (var i = 0; i < segments; i++)
        {
            var t = i / (double)segments * 2 * Math.PI;
            branch.Add(new Point(branchCenter.X + Math.Cos(t) * branchRadius,
                                 branchCenter.Y + Math.Sin(t) * branchRadius * 0.6));
        }
        return (pipe, branch);
    }

    /// <summary>
    /// 3D-Einblendung "Muffenversatz" (btnViewType3) - 2 parallele Ringe + Versatz-Vektor.
    /// Liefert die zwei Ringe als Polylines + Versatz-Linie als Punktepaar.
    /// </summary>
    public static (IReadOnlyList<Point> Ring1, IReadOnlyList<Point> Ring2, Point OffsetA, Point OffsetB)
        Build3DJointOffset(Point ring1Center, double ring1Radius,
                           Point ring2Center, double ring2Radius, int segments = 24)
    {
        var r1 = new List<Point>(segments);
        var r2 = new List<Point>(segments);
        for (var i = 0; i < segments; i++)
        {
            var t = i / (double)segments * 2 * Math.PI;
            r1.Add(new Point(ring1Center.X + Math.Cos(t) * ring1Radius,
                             ring1Center.Y + Math.Sin(t) * ring1Radius));
            r2.Add(new Point(ring2Center.X + Math.Cos(t) * ring2Radius,
                             ring2Center.Y + Math.Sin(t) * ring2Radius));
        }
        return (r1, r2, ring1Center, ring2Center);
    }

    // ── Quantifizierungen ────────────────────────────────────────────────

    /// <summary>
    /// Berechnet die Distanz zwischen zwei Punkten auf einer projizierten Rohrwand
    /// (btnMeasurePipe Verbesserung). Nimmt die einfache zylindrische Projektion an:
    /// Punkte werden vom Bildraum auf den Zylindermantel rueckprojiziert.
    ///
    /// Eingabe: 2 Bildpunkte, Rohrzentrum, Rohrradius (in Bildpixeln), Pipe-Diameter in mm.
    /// Liefert: Distanz auf der Rohrwand in mm.
    /// </summary>
    public static double MeasurePipeSurfaceDistanceMm(
        Point a, Point b, Point pipeCenter, double pipeRadiusPx, double pipeDiameterMm)
    {
        if (pipeRadiusPx <= 0) throw new ArgumentException("pipeRadiusPx muss > 0 sein.");
        if (pipeDiameterMm <= 0) throw new ArgumentException("pipeDiameterMm muss > 0 sein.");

        // Bogenwinkel zwischen den beiden Punkten am Rohrumfang (in Radian)
        var ax = a.X - pipeCenter.X; var ay = a.Y - pipeCenter.Y;
        var bx = b.X - pipeCenter.X; var by = b.Y - pipeCenter.Y;
        var angA = Math.Atan2(ay, ax);
        var angB = Math.Atan2(by, bx);
        var dAng = Math.Abs(angA - angB);
        if (dAng > Math.PI) dAng = 2 * Math.PI - dAng;

        // Umfangs-Distanz in mm
        var arcMm = dAng * pipeDiameterMm / 2.0;

        // Axiale Komponente (Verschiebung in Rohrachse) -- approximiert ueber radialen Versatz
        var radialA = Math.Sqrt(ax * ax + ay * ay);
        var radialB = Math.Sqrt(bx * bx + by * by);
        var axialPx = Math.Abs(radialA - radialB);
        // Axiale Skalierung: 1px ~= pipeDiameterMm / (2 * pipeRadiusPx)
        var axialMm = axialPx * pipeDiameterMm / (2.0 * pipeRadiusPx);

        return Math.Sqrt(arcMm * arcMm + axialMm * axialMm);
    }
}
