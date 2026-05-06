using System;
using System.Globalization;
using System.Windows;

namespace AuswertungPro.Next.UI.Ai.PhotoAssistant;

/// <summary>
/// Werkzeug "Anschluss" (BCA) — Mondsichel-Schablone als WPF-Path.
///
/// Form:
///   - Sichel-Breite (Spitze zu Spitze): sw = 0.65 * baseR * latRel
///   - Bauch-Tiefe: sh = 0.30 * baseR * latRel * (1 - |angleSkew|*0.4)
///   - angleSkew = (lateralAngle - 90) / 60   (Bereich -1 bei 30°, 0 bei 90°, +1 bei 150°)
///   - skewOffset = sw * 0.35 * angleSkew
///   - outerSagitta = sh + skewOffset * 0.3
///   - outerR = (sw/2)^2 + outerSagitta^2 / (2 * outerSagitta)
///   - innerSagitta = sh * 0.55 - skewOffset * 0.3
///   - Wenn innerSagitta &gt; 0: konkaver Innenbogen (echte Sichel)
///     Sonst: gerade Linie statt Innenbogen (D-Form)
/// </summary>
public static class LateralToolService
{
    /// <summary>
    /// Liefert den Sichel-SVG-/WPF-Path-Daten-String (Path.Data),
    /// vor der Transformation translate(cx, cy) rotate(hour*30).
    /// </summary>
    public static string BuildSichelPathData(
        double baseRadius,
        double lateralRelative,         // (DN%/100) * latScale
        double lateralAngleDegrees)
    {
        var latRel = Math.Max(0.05, lateralRelative);
        var angleSkew = (lateralAngleDegrees - 90.0) / 60.0;
        if (angleSkew < -1) angleSkew = -1;
        if (angleSkew > 1) angleSkew = 1;

        var sw = 0.65 * baseRadius * latRel;
        var sh = 0.30 * baseRadius * latRel * (1 - Math.Abs(angleSkew) * 0.4);
        var skewOffset = sw * 0.35 * angleSkew;

        var outerSagitta = sh + skewOffset * 0.3;
        if (outerSagitta < 1e-6) outerSagitta = 1e-6;
        var outerR = ((sw / 2.0) * (sw / 2.0) + outerSagitta * outerSagitta) / (2.0 * outerSagitta);

        var innerSagitta = sh * 0.55 - skewOffset * 0.3;

        var halfSw = sw / 2.0;
        var ic = CultureInfo.InvariantCulture;

        if (innerSagitta > 0)
        {
            var innerR = ((sw / 2.0) * (sw / 2.0) + innerSagitta * innerSagitta) / (2.0 * innerSagitta);
            return string.Format(ic,
                "M {0:F3},0 A {1:F3},{1:F3} 0 0 1 {2:F3},0 A {3:F3},{3:F3} 0 0 0 {0:F3},0 Z",
                -halfSw, outerR, halfSw, innerR);
        }
        else
        {
            return string.Format(ic,
                "M {0:F3},0 A {1:F3},{1:F3} 0 0 1 {2:F3},0 L {0:F3},0 Z",
                -halfSw, outerR, halfSw);
        }
    }

    /// <summary>
    /// Berechnet die Sichel-Position (Mittelpunkt) auf dem Hauptrohr.
    /// </summary>
    public static Point ComputeSichelCenter(
        Point pipeCenter,
        double baseRadius,
        int hour,
        double latOffsetX = 0,
        double latOffsetY = 0)
    {
        var hourAngle = (hour * 30.0 - 90.0) * Math.PI / 180.0;
        var x = pipeCenter.X + Math.Cos(hourAngle) * 0.65 * baseRadius + latOffsetX;
        var y = pipeCenter.Y + Math.Sin(hourAngle) * 0.65 * baseRadius + latOffsetY;
        return new Point(x, y);
    }

    /// <summary>Rotation in Grad: jede Uhrposition ist 30° Drehung.</summary>
    public static double RotationDegrees(int hour) => hour * 30.0;

    /// <summary>Begrenzt Mausrad-Zoom auf [0.3, 2.5].</summary>
    public static double ClampScale(double scale) => scale switch
    {
        < 0.3 => 0.3,
        > 2.5 => 2.5,
        _ => scale
    };

    /// <summary>Internes Berechnungs-Resultat fuer Tests.</summary>
    public sealed record SichelMath(
        double Sw,
        double Sh,
        double AngleSkew,
        double SkewOffset,
        double OuterSagitta,
        double InnerSagitta,
        double OuterR);

    /// <summary>Liefert die berechneten Sichel-Werte (fuer Tests + Diagnose).</summary>
    public static SichelMath ComputeMath(double baseRadius, double lateralRelative, double lateralAngleDegrees)
    {
        var latRel = Math.Max(0.05, lateralRelative);
        var angleSkew = (lateralAngleDegrees - 90.0) / 60.0;
        if (angleSkew < -1) angleSkew = -1;
        if (angleSkew > 1) angleSkew = 1;

        var sw = 0.65 * baseRadius * latRel;
        var sh = 0.30 * baseRadius * latRel * (1 - Math.Abs(angleSkew) * 0.4);
        var skewOffset = sw * 0.35 * angleSkew;
        var outerSagitta = sh + skewOffset * 0.3;
        var innerSagitta = sh * 0.55 - skewOffset * 0.3;
        var outerR = outerSagitta > 1e-6
            ? ((sw / 2.0) * (sw / 2.0) + outerSagitta * outerSagitta) / (2.0 * outerSagitta)
            : double.PositiveInfinity;

        return new SichelMath(sw, sh, angleSkew, skewOffset, outerSagitta, innerSagitta, outerR);
    }
}
