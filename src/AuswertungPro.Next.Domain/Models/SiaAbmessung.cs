using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Bringt eine ABMESSUNG auf Millimeter als ganze Zahl, wie SIA405 sie verlangt.
///
/// Nur fuer Abmessungen — also Bauteilquerschnitte. Die Norm fuehrt dafuer den Typ
/// <c>SIA405_Base_Abwasser.Abmessung</c> in Millimetern: Dimension1/2, Lichte_Hoehe,
/// Deckel.Durchmesser.
///
/// NICHT fuer Hoehen und Laengen. Die tragen den Typ <c>Base_LV95.Hoehe</c>
/// beziehungsweise eine Laenge und stehen in METERN: Sohlenkote, Deckel.Kote,
/// LaengeEffektiv. Die Schachttiefe gehoert ebenfalls dorthin — sie ist eine Hoehe,
/// kein Querschnitt, und hat in SIA405 ohnehin kein Zielfeld. Wer diese Klasse
/// darauf anwendet, macht aus 2.02 m die Zahl 2020 und damit einen Faktor-1000-Fehler
/// in der anderen Richtung.
///
/// Warum das nicht einfach mal 1000 ist: Im Programm stehen zwei Einheiten
/// nebeneinander. Schachtmasse sind Meter ("1.00"), Rohrnennweiten Millimeter
/// ("200"). Ein pauschales mal 1000 waere selbst ein Faktor-1000-Fehler.
///
/// Die Regel stammt nicht von hier, sondern aus der SchachtPro-Zeichnung: Ein Wert
/// ueber 10 gilt bereits als Millimeter. Die Grenze ist unbedenklich, weil es weder
/// einen Schacht mit 10 m Durchmesser noch einen mit 1 cm gibt. Wichtig ist vor
/// allem der Fall "1000": Wer im Meterfeld 1000 statt 1.00 tippt, sieht in der App
/// keinen Fehler, weil die Zeichnung denselben Wert durch 1000 teilt. Ein stur
/// multiplizierender Konverter machte daraus 1'000'000 mm - und zwar genau bei den
/// Protokollen, bei denen nie etwas auffiel.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SiaAbmessung
{
    /// <summary>Ab hier gilt ein Wert als bereits in Millimetern angegeben.</summary>
    private const double MillimeterAb = 10d;

    private static readonly Regex Zahl = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// Millimeter als ganze Zahl, oder <c>null</c>, wenn der Wert keine brauchbare
    /// Angabe enthaelt. Dann wird nichts geschrieben statt geraten.
    /// </summary>
    public static int? NachMillimeter(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;

        // Zusaetze wie "DN 200" oder "250 mm" sind in Bestandsdaten ueblich.
        var treffer = Zahl.Match(text);
        if (!treffer.Success)
            return null;

        if (!double.TryParse(
                treffer.Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var zahl))
        {
            return null;
        }

        if (zahl <= 0)
            return null;

        var mm = zahl > MillimeterAb ? zahl : zahl * 1000d;
        var gerundet = (int)Math.Round(mm, MidpointRounding.AwayFromZero);
        return gerundet > 0 ? gerundet : null;
    }

    /// <summary>
    /// Trennzeichen eines Wertepaars. Der Schraegstrich stammt aus SchachtPro
    /// ("0.60/1.00"), das Mal-Zeichen aus bestehenden Projekten: In Zone 1.15 steht
    /// bei 36 Schaechten "900 x 1100 mm". Ohne das x waere daraus 900/900 geworden.
    /// </summary>
    private static readonly char[] Paartrenner = ['/', 'x', 'X', '×', '*'];

    /// <summary>
    /// Verteilt eine Angabe auf zwei Masse. "0.60/1.00" und "900 x 1100 mm" ergeben
    /// zwei Werte, ein einzelner Wert ergibt zweimal dasselbe — bei einem runden
    /// Schacht sind Dimension1 und Dimension2 gleich, so schreibt es auch der
    /// AWU-Export.
    /// </summary>
    public static (int? Erstes, int? Zweites) NachMillimeterPaar(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return (null, null);

        var teile = text.Split(Paartrenner, StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length >= 2)
            return (NachMillimeter(teile[0]), NachMillimeter(teile[1]));

        var einzeln = NachMillimeter(text);
        return (einzeln, einzeln);
    }
}
