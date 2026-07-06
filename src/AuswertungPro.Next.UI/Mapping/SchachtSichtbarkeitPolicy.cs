namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Entscheidet, ob die Schaechte (Kreise) gezeichnet werden: nur wenn eingeschaltet UND weit
/// genug reingezoomt ("erst beim Reinzoomen", wie der QGIS-Schaechte-Layer). So bleibt die
/// Uebersicht bei kleinem Zoom ruhig und die Karte schnell (weniger Symbole gleichzeitig).
/// </summary>
public static class SchachtSichtbarkeitPolicy
{
    /// <summary>Bis zu dieser Aufloesung (m/px in WebMercator) — und feiner — werden Schaechte gezeigt.</summary>
    public const double MaxAufloesungMeterProPixel = 5.0;

    public static bool ShouldShow(bool eingeschaltet, double aufloesungMeterProPixel)
        => eingeschaltet
           && aufloesungMeterProPixel > 0
           && aufloesungMeterProPixel <= MaxAufloesungMeterProPixel;
}
