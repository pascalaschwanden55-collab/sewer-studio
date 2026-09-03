using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Die zweite Dimension einer Haltung nach SIA405.
///
/// Die Haltung selbst kennt nur <c>Lichte_Hoehe</c>. Die Breite steckt am Rohrprofil als
/// <c>HoehenBreitenverhaeltnis</c> (Hoehe geteilt durch Breite, Bereich 0.00001 bis 100;
/// in 2020 und 2020_1 gleich). Ein Rechteckkanal 1000 hoch und 600 breit ist also
/// Lichte_Hoehe 1000 mit Rechteckprofil und Verhaeltnis 1.66667; ein Normei 600/900 ist
/// Lichte_Hoehe 900 mit Eiprofil und Verhaeltnis 1.5.
///
/// Im Programm stehen Hoehe in <see cref="FieldKeys.NominalDiameterMm"/> und Breite in
/// <see cref="FieldKeys.ClearWidthMm"/>. Rund heisst: beide gleich oder Breite leer. Dann
/// gibt es kein Verhaeltnis, so haelt es auch der Kantonsexport.
///
/// Reine Rechnung, hin und zurueck, damit Export und Import dieselbe Zahl sehen.
/// </summary>
public static class XtfRohrprofilVerhaeltnis
{
    /// <summary>Das Attribut am <c>Rohrprofil</c>, wie es im Modell heisst.</summary>
    public const string Attribut = "HoehenBreitenverhaeltnis";

    private const double Minimum = 0.00001d;
    private const double Maximum = 100d;

    /// <summary>
    /// Das Verhaeltnis fuer die Datei, oder <c>null</c>, wenn keines geschrieben werden
    /// darf: Hoehe oder Breite fehlen, sind keine Zahlen, oder beide sind gleich (rund).
    /// Fuenf Nachkommastellen, wie das Modell sie zulaesst.
    /// </summary>
    public static string? Berechne(string? hoehe, string? breite)
    {
        var h = SiaAbmessung.NachMillimeter(hoehe);
        var b = SiaAbmessung.NachMillimeter(breite);
        if (h is not > 0 || b is not > 0 || h == b)
            return null;

        var verhaeltnis = (double)h.Value / b.Value;
        if (verhaeltnis < Minimum || verhaeltnis > Maximum)
            return null;

        return Math.Round(verhaeltnis, 5).ToString("0.#####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// True, wenn die zwei Programmwerte eindeutig ein rundes Profil beschreiben:
    /// eine gueltige Hoehe und entweder keine Breite oder dieselbe Breite. Ungueltige
    /// Texte gelten nicht als rund, damit sie nie das vorhandene Verhaeltnis loeschen.
    /// </summary>
    public static bool IstRund(string? hoehe, string? breite)
    {
        var h = SiaAbmessung.NachMillimeter(hoehe);
        if (h is not (> 0 and <= 99_999))
            return false;

        if (string.IsNullOrWhiteSpace(breite))
            return true;

        var b = SiaAbmessung.NachMillimeter(breite);
        return b is > 0 and <= 99_999 && h == b;
    }

    /// <summary>
    /// Die Breite in Millimetern aus Hoehe und Verhaeltnis der Datei, oder <c>null</c>,
    /// wenn eines von beiden fehlt oder unbrauchbar ist.
    /// </summary>
    public static int? Breite(string? hoehe, string? verhaeltnis)
    {
        var h = SiaAbmessung.NachMillimeter(hoehe);
        if (h is not > 0)
            return null;

        var text = (verhaeltnis ?? "").Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            || v < Minimum || v > Maximum)
        {
            return null;
        }

        var breite = (int)Math.Round(h.Value / v, MidpointRounding.AwayFromZero);
        return breite > 0 ? breite : null;
    }

    /// <summary>True, wenn das Verhaeltnis der Datei dasselbe sagt wie Hoehe und Breite.</summary>
    public static bool Gleich(string? ausDerDatei, string? berechnet)
    {
        var a = (ausDerDatei ?? "").Trim().Replace(',', '.');
        var b = (berechnet ?? "").Trim();
        if (a.Length == 0 || b.Length == 0)
            return a.Length == b.Length;

        return double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
               && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
               && Math.Abs(x - y) < 0.00001d;
    }
}
