using System.Globalization;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Waehlt je Haltung genau eine VSA-KEK-Untersuchung. Bis 30.09.2026 ueberschrieb die
/// zweite Untersuchung derselben Haltung (z.B. die Gegenbefahrung) Datum, Laenge,
/// Richtung, Video, Herkunft und Bemerkung der ersten, und der Datensatz trug die Befunde
/// beider. Jetzt gilt dieselbe Regel wie beim WinCan-Import
/// (<see cref="UntersuchungsAuswahl"/>): neuestes glaubwuerdiges Datum, bei Gleichstand
/// die Dateireihenfolge. Die XTF kennt keinen technischen Zeitstempel als Rueckfall.
/// </summary>
internal static class VsaKekUntersuchungsWahl
{
    /// <summary>Die Untersuchungen einer Haltung: die uebernommene und die uebrigen.</summary>
    internal sealed record Gruppe<T>(string Haltung, T Gewaehlt, IReadOnlyList<T> Uebersprungen);

    private static readonly string[] Zeitformate =
    {
        "yyyyMMdd", "yyyyMMddHHmmss", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "dd.MM.yyyy"
    };

    /// <summary>
    /// Gruppiert die Haltungsuntersuchungen nach Bezeichnung, verglichen wie die Uebernahme
    /// den Projektdatensatz findet (<see cref="HoldingKeyNormalizer.Normalize"/>, Gross-/
    /// Kleinschreibung egal). Die Gruppen stehen in der Reihenfolge ihres ersten Auftretens.
    /// </summary>
    public static List<Gruppe<T>> Waehle<T>(
        IEnumerable<T> haltungsuntersuchungen,
        Func<T, string> bezeichnung,
        Func<T, string> zeitpunkt)
    {
        ArgumentNullException.ThrowIfNull(haltungsuntersuchungen);

        var reihenfolge = new List<string>();
        var jeHaltung = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var untersuchung in haltungsuntersuchungen)
        {
            var schluessel = HoldingKeyNormalizer.Normalize(bezeichnung(untersuchung));
            if (!jeHaltung.TryGetValue(schluessel, out var liste))
            {
                liste = new List<T>();
                jeHaltung[schluessel] = liste;
                reihenfolge.Add(schluessel);
            }

            liste.Add(untersuchung);
        }

        return reihenfolge
            .Select(schluessel =>
            {
                var geordnet = UntersuchungsAuswahl.Ordne(jeHaltung[schluessel],
                    u => UntersuchungsAuswahl.Sortierschluessel(new[] { LiesZeitpunkt(zeitpunkt(u)) }));
                return new Gruppe<T>(schluessel, geordnet[0], geordnet.Skip(1).ToList());
            })
            .ToList();
    }

    /// <summary>Liest den Rohwert von <c>Zeitpunkt</c>; ein unlesbarer Wert ergibt <c>null</c>.</summary>
    public static DateTime? LiesZeitpunkt(string? roh)
        => DateTime.TryParseExact((roh ?? "").Trim(), Zeitformate, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var datum)
            ? datum
            : null;

    /// <summary>
    /// Das Datum im Importbericht. Ein Platzhalter heisst ausdruecklich so und wird nicht
    /// als Aufnahmedatum ausgegeben.
    /// </summary>
    public static string Datumstext(string? roh)
    {
        var datum = LiesZeitpunkt(roh);
        if (UntersuchungsAuswahl.IstVorgabetag(datum))
            return "Platzhalterdatum 31.12.2007 (kein glaubwürdiges Untersuchungsdatum)";
        if (datum is { } d && UntersuchungsAuswahl.Glaubwuerdig(d) is null)
            return $"Platzhalterdatum {d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} (vor 1990, kein glaubwürdiges Untersuchungsdatum)";
        if (datum is { } gueltig)
            return gueltig.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(roh) ? "ohne Datum" : $"«{roh.Trim()}» (Datum nicht lesbar)";
    }

    /// <summary>Die Meldung fuer eine nicht gewaehlte Untersuchung.</summary>
    public static string Meldung(string haltung, int anzahl,
        string gewaehltTid, string gewaehltZeitpunkt,
        string uebersprungenTid, string uebersprungenZeitpunkt, int befunde)
        => $"Haltung \"{haltung}\": Datei führt {anzahl} Untersuchungen. "
           + $"Übernommen: {Datumstext(gewaehltZeitpunkt)} (TID {gewaehltTid}); "
           + $"übersprungen: {Datumstext(uebersprungenZeitpunkt)} (TID {uebersprungenTid}) mit {befunde} Befunden.";
}
