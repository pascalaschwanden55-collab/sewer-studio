using System;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Mathematik fuer die PipeGraph-Timeline-Skala: Intervall-Auswahl und Meter ↔ Pixel-Konversion.
/// Aus <see cref="AuswertungPro.Next.UI.Controls.PipeGraphTimeline"/> extrahiert (verhaltensneutral),
/// damit unit-testbar ohne WPF-Abhaengigkeit.
/// </summary>
public static class TimelineScaleCalculator
{
    /// <summary>
    /// Waehlt ein sinnvolles Beschriftungs-Intervall (Meter) fuer die Timeline-Skala,
    /// sodass die Anzahl der Ticks uebersichtlich bleibt.
    /// </summary>
    /// <param name="totalLength">Gesamtlaenge der Haltung in Metern (muss positiv sein).</param>
    /// <returns>Meter-Intervall zwischen zwei Skalen-Ticks.</returns>
    public static double ChooseInterval(double totalLength) => totalLength switch
    {
        <= 0  => 1,
        <= 10  => 2,
        <= 25  => 5,
        <= 50  => 10,
        <= 100 => 20,
        <= 250 => 50,
        _      => 100
    };

    /// <summary>
    /// Rechnet eine Meter-Position in eine Canvas-X-Koordinate um.
    /// Gibt 0 zurueck, wenn <paramref name="canvasWidth"/> oder <paramref name="totalLength"/> nicht positiv sind.
    /// </summary>
    /// <param name="meter">Meter-Position (darf ausserhalb [0, totalLength] liegen).</param>
    /// <param name="totalLength">Gesamtlaenge der Haltung in Metern.</param>
    /// <param name="canvasWidth">Breite des Zeichenbereichs in Pixeln.</param>
    /// <returns>X-Koordinate in Pixeln, geclamped auf [0, canvasWidth].</returns>
    public static double MeterToX(double meter, double totalLength, double canvasWidth)
    {
        if (canvasWidth <= 0 || totalLength <= 0)
            return 0;

        return Math.Clamp(meter / totalLength, 0, 1) * canvasWidth;
    }

    /// <summary>
    /// Rechnet eine Canvas-X-Koordinate in eine Meter-Position um.
    /// Gibt 0 zurueck, wenn <paramref name="canvasWidth"/> oder <paramref name="totalLength"/> nicht positiv sind.
    /// </summary>
    /// <param name="x">X-Koordinate in Pixeln.</param>
    /// <param name="totalLength">Gesamtlaenge der Haltung in Metern.</param>
    /// <param name="canvasWidth">Breite des Zeichenbereichs in Pixeln.</param>
    /// <returns>Meter-Position, geclamped auf [0, totalLength].</returns>
    public static double XToMeter(double x, double totalLength, double canvasWidth)
    {
        if (canvasWidth <= 0 || totalLength <= 0)
            return 0;

        return Math.Clamp((x / canvasWidth) * totalLength, 0, totalLength);
    }
}
