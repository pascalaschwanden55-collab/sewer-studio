using System;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Koordinatentransformation LV95 (EPSG:2056) -> WebMercator (EPSG:3857) fuer die Kartendarstellung.
/// </summary>
public static class CoordinateTransform
{
    // Naeherung fuer Kartendarstellung (swisstopo-Approx-Formel), NICHT fuer Vermessung.
    //
    // Quelle: Swisstopo "Approximate formulas for the transformation between Swiss
    //         projection coordinates and WGS84" — implementiert in
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
    //   Y = ln(tan((90+lat)*PI/360)) * 20037508.342789244 / 180

    private const double R = 20037508.342789244; // Halbumfang Erde in Metern (WebMercator)

    /// <summary>
    /// Konvertiert LV95/CH1903+ Koordinaten (EPSG:2056) in WebMercator (EPSG:3857).
    /// Naeherungsgenauigkeit: ca. 1 m — ausreichend fuer Kartendarstellung.
    /// </summary>
    /// <param name="e">Ostwert (E-Koordinate, typisch ~2'600'000 bis ~2'830'000)</param>
    /// <param name="n">Nordwert (N-Koordinate, typisch ~1'070'000 bis ~1'300'000)</param>
    /// <returns>WebMercator (X, Y) in Metern</returns>
    public static (double X, double Y) Lv95ToWebMercator(double e, double n)
    {
        // --- Schritt 1: LV95 -> WGS84 ---
        double y = (e - 2_600_000.0) / 1_000_000.0;
        double x = (n - 1_200_000.0) / 1_000_000.0;

        double lonArcsec = 2.6779094
            + 4.728982 * y
            + 0.791484 * y * x
            + 0.1306   * y * x * x
            - 0.0436   * y * y * y;

        double latArcsec = 16.9023892
            + 3.238272 * x
            - 0.270978 * y * y
            - 0.002528 * x * x
            - 0.0447   * y * y * x
            - 0.0140   * x * x * x;

        // Umrechnung von "10000 Bogensekunden"-Einheit in Dezimalgrad
        double lonDeg = lonArcsec * 100.0 / 36.0;
        double latDeg = latArcsec * 100.0 / 36.0;

        // --- Schritt 2: WGS84 -> WebMercator (exakt) ---
        double mercX = lonDeg * R / 180.0;
        double mercY = Math.Log(Math.Tan((90.0 + latDeg) * Math.PI / 360.0)) * R / 180.0;

        return (mercX, mercY);
    }
}
