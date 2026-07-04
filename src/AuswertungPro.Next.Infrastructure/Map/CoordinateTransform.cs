using System;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Koordinatentransformation LV95 (EPSG:2056) -> WGS84/WebMercator fuer die Kartendarstellung.
/// </summary>
public static class CoordinateTransform
{
    // Naeherung fuer Kartendarstellung (swisstopo-Approx-Formel), NICHT fuer Vermessung.
    //
    // Quelle: Swisstopo "Approximate formulas for the transformation between Swiss
    //         projection coordinates and WGS84" - implementiert in
    //         https://github.com/antistatique/swisstopo (MIT, konsistent mit offizieller Doku).
    //
    // Schritt 1: LV95 -> WGS84 (lon/lat in Grad) via swisstopo Polynomformel
    //   y_aux = (E - 2'600'000) / 1'000'000
    //   x_aux = (N - 1'200'000) / 1'000'000
    //   lon [deg] = (2.6779094 + 4.728982*y + 0.791484*y*x + 0.1306*y*x^2 - 0.0436*y^3) * 100/36
    //   lat [deg] = (16.9023892 + 3.238272*x - 0.270978*y^2 - 0.002528*x^2
    //                - 0.0447*y^2*x - 0.0140*x^3) * 100/36
    //
    // Schritt 2: WGS84 -> WebMercator (EPSG:3857, exakt)
    //   X = lon * 20037508.342789244 / 180
    //   Y = ln(tan((90+lat)*PI/360)) * R_earth

    private const double R = 6378137.0;
    private const double HalfCirc = 20037508.342789244;

    /// <summary>
    /// Konvertiert LV95/CH1903+ Koordinaten (EPSG:2056) in WebMercator (EPSG:3857).
    /// Naeherungsgenauigkeit: ca. 1 m - ausreichend fuer Kartendarstellung.
    /// </summary>
    public static (double X, double Y) Lv95ToWebMercator(double e, double n)
    {
        var (lonDeg, latDeg) = Lv95ToWgs84(e, n);

        double mercX = lonDeg * HalfCirc / 180.0;
        double mercY = Math.Log(Math.Tan((90.0 + latDeg) * Math.PI / 360.0)) * R;

        return (mercX, mercY);
    }

    /// <summary>
    /// Konvertiert LV95/CH1903+ Koordinaten (EPSG:2056) in WGS84 (EPSG:4326).
    /// Rueckgabe: Longitude, Latitude in Dezimalgrad. Naeherungsgenauigkeit: ca. 1 m.
    /// </summary>
    public static (double Lon, double Lat) Lv95ToWgs84(double e, double n)
    {
        double y = (e - 2_600_000.0) / 1_000_000.0;
        double x = (n - 1_200_000.0) / 1_000_000.0;

        double lonArcsec = 2.6779094
            + 4.728982 * y
            + 0.791484 * y * x
            + 0.1306 * y * x * x
            - 0.0436 * y * y * y;

        double latArcsec = 16.9023892
            + 3.238272 * x
            - 0.270978 * y * y
            - 0.002528 * x * x
            - 0.0447 * y * y * x
            - 0.0140 * x * x * x;

        double lonDeg = lonArcsec * 100.0 / 36.0;
        double latDeg = latArcsec * 100.0 / 36.0;

        return (lonDeg, latDeg);
    }
}
