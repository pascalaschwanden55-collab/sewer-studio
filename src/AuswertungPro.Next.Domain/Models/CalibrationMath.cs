using System;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Pure-static Geometrie- und Umrechnungsfunktionen fuer Rohr-Kalibrierung.
/// Alle Parameter werden explizit uebergeben – kein Instanzstatus, keine IO-Abhaengigkeiten.
/// </summary>
public static class CalibrationMath
{
    /// <summary>
    /// Millimeter pro normierter Einheit (Bildbreite = 1.0).
    /// Gibt 0 zurueck wenn <paramref name="normalizedDiameter"/> nicht positiv ist.
    /// </summary>
    /// <param name="nominalDiameterMm">Nennweite des Rohrs in Millimeter (z.B. 300 fuer DN300).</param>
    /// <param name="normalizedDiameter">Rohrdurchmesser als normierter Wert (0.0–1.0 relativ zur Bildbreite).</param>
    public static double MmPerNormUnit(int nominalDiameterMm, double normalizedDiameter)
        => normalizedDiameter > 0 ? nominalDiameterMm / normalizedDiameter : 0.0;

    /// <summary>
    /// Normierte Laenge in Millimeter umrechnen.
    /// Fallback auf <c>normalizedLength * 500</c> wenn kein normierter Durchmesser bekannt ist.
    /// </summary>
    /// <param name="normalizedLength">Zu konvertierende Laenge (normiert, 0.0–1.0).</param>
    /// <param name="nominalDiameterMm">Nennweite des Rohrs in Millimeter.</param>
    /// <param name="normalizedDiameter">Rohrdurchmesser als normierter Wert.</param>
    public static double NormToMm(double normalizedLength, int nominalDiameterMm, double normalizedDiameter)
    {
        if (normalizedDiameter <= 0) return normalizedLength * 500; // Fallback
        return normalizedLength * MmPerNormUnit(nominalDiameterMm, normalizedDiameter);
    }

    /// <summary>
    /// Pixel (normiert relativ zur Bildbreite) in Millimeter umrechnen.
    /// Bevorzugt <paramref name="normalizedDiameter"/> wenn vorhanden;
    /// faellt sonst auf <paramref name="pipePixelDiameter"/> / <paramref name="frameWidthPx"/> zurueck.
    /// </summary>
    /// <param name="normalizedPixels">Zu konvertierende Pixelanzahl (normiert).</param>
    /// <param name="frameWidthPx">Absolute Frame-Breite in Pixeln (fuer Pixel→Norm-Umrechnung).</param>
    /// <param name="nominalDiameterMm">Nennweite des Rohrs in Millimeter.</param>
    /// <param name="normalizedDiameter">Rohrdurchmesser als normierter Wert (0.0–1.0).</param>
    /// <param name="pipePixelDiameter">Rohrdurchmesser in absoluten Canvas-Pixeln (Fallback).</param>
    public static double PixelToMm(
        double normalizedPixels,
        double frameWidthPx,
        int nominalDiameterMm,
        double normalizedDiameter,
        double pipePixelDiameter)
    {
        if (normalizedDiameter > 0)
            return NormToMm(normalizedPixels, nominalDiameterMm, normalizedDiameter);

        if (pipePixelDiameter <= 0) return 0;

        double pipePixelNormalized = pipePixelDiameter / frameWidthPx;
        double mmPerNormPixel = nominalDiameterMm / pipePixelNormalized;
        return normalizedPixels * mmPerNormPixel;
    }

    /// <summary>
    /// Aspect-Ratio-korrigierte Distanz zwischen zwei normierten Punkten.
    /// Normierte Koordinaten: X=0..1 ueber Bildbreite, Y=0..1 ueber Bildhoehe.
    /// Bei nicht-quadratischen Bildern wird X mit dem Seitenverhaeltnis (W/H) skaliert.
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

    /// <summary>
    /// Aspect-korrigierte normierte Laenge in Millimeter umrechnen.
    /// </summary>
    /// <param name="a">Startpunkt (normiert).</param>
    /// <param name="b">Endpunkt (normiert).</param>
    /// <param name="nominalDiameterMm">Nennweite des Rohrs in Millimeter.</param>
    /// <param name="normalizedDiameter">Rohrdurchmesser als normierter Wert.</param>
    /// <param name="imageAspect">Seitenverhaeltnis (Breite/Hoehe).</param>
    public static double NormToMmAspect(
        NormalizedPoint a,
        NormalizedPoint b,
        int nominalDiameterMm,
        double normalizedDiameter,
        double imageAspect = 1.0)
    {
        double dist = AspectCorrectedDistance(a, b, imageAspect);
        return NormToMm(dist, nominalDiameterMm, normalizedDiameter);
    }

    /// <summary>
    /// Konvertiert einen Punkt auf dem Frame in eine Uhrposition (0.0–12.0).
    /// 0.0/12.0 = Scheitel (oben, 12 Uhr), 3.0 = rechts, 6.0 = Sohle (unten), 9.0 = links.
    /// </summary>
    /// <param name="point">Punkt auf dem Frame (normiert, 0.0–1.0).</param>
    /// <param name="pipeCenter">Rohrmitte (normiert).</param>
    public static double PointToClockHour(NormalizedPoint point, NormalizedPoint pipeCenter)
    {
        double dx = point.X - pipeCenter.X;
        double dy = point.Y - pipeCenter.Y;
        // atan2: 0° = rechts, wir wollen 0° = oben (12 Uhr)
        double angleRad = Math.Atan2(dx, -dy); // -dy weil Y nach unten waechst
        double angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360;
        return angleDeg / 30.0; // 360° / 12 Stunden = 30° pro Stunde
    }
}
