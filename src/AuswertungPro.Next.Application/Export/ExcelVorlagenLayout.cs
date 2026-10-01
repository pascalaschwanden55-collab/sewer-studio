namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Die festen Zeilen der beiden Export-Vorlagen. Beide Blaetter sind gleich
/// aufgebaut, damit sie gleich aussehen und nur eine Stelle gepflegt werden muss.
///
/// Vorher standen diese Zeilennummern an drei Stellen als blosse Zahl: im
/// Vorlagenleser der Schachtseite und zweimal als Aufrufargument im Export.
/// Verschiebt sich die Kopfzeile und eine der drei Stellen wird vergessen, liest
/// der Export stillschweigend die falsche Zeile - ohne Fehlermeldung.
///
/// Erzeugt werden die Vorlagen von <c>tools/ExcelVorlagenBauer/vorlage.py</c>;
/// die Werte hier muessen zu den dortigen Angaben passen.
/// </summary>
public static class ExcelVorlagenLayout
{
    /// <summary>Zelle A dieser Zeile traegt den Berichtstitel (gruenes Band).</summary>
    public const int TitelZeile = 25;

    /// <summary>Zeile mit den Spaltenueberschriften.</summary>
    public const int KopfZeile = 26;

    /// <summary>Erste Datenzeile - zugleich die gestaltete Musterzeile.</summary>
    public const int ErsteDatenZeile = 27;

    /// <summary>
    /// Bis hierhin reichen Zaehlformeln und bedingte Formatierung in der Vorlage.
    /// Mehr Zeilen wuerden von den Kennzahlen nicht mehr erfasst.
    /// </summary>
    public const int LetzteFormelZeile = 5000;

    /// <summary>Wie viele Datenzeilen die Vorlage traegt.</summary>
    public const int MaximaleDatenzeilen = LetzteFormelZeile - ErsteDatenZeile + 1;

    /// <summary>
    /// Anzeigegroesse des Logos in Bildpunkten.
    ///
    /// ClosedXML setzt Bilder beim Speichern auf ihre native Pixelgroesse
    /// zurueck - aus 5,6 cm wurden 8,7 cm, und das Logo lag ueber der Legende.
    /// Die Groesse muss deshalb nach dem Fuellen erneut gesetzt werden. Die
    /// Werte entsprechen den 5,6 cm aus tools/ExcelVorlagenBauer/werkzeug.py.
    /// </summary>
    public const int LogoBreitePixel = 212;

    /// <inheritdoc cref="LogoBreitePixel"/>
    public const int LogoHoehePixel = 87;
}
