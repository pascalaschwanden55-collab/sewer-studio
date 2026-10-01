using System.Globalization;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Waehlt je Haltung die Haupt-Untersuchung einer VSA-KEK-Datei. Bis 30.09.2026
/// ueberschrieb die zweite Untersuchung derselben Haltung (z.B. die Gegenbefahrung) Datum,
/// Laenge, Richtung, Video, Herkunft und Bemerkung der ersten, und der Datensatz trug die
/// Befunde beider.
///
/// Regel (Entscheid Pascal 30.09.2026, «Variante C»): Haupt-Untersuchung ist die
/// vollstaendigste, nicht die neueste —
/// 1. nicht abgebrochen vor abgebrochen (ein Kanalschaden mit Abbruchcode BDC*; die Datei
///    kennt kein eigenes Abbruchfeld),
/// 2. laengere untersuchte Strecke,
/// 3. erst dann das glaubwuerdige Datum wie bei WinCan (<see cref="UntersuchungsAuswahl"/>),
/// 4. bei Gleichstand die erste in der Datei.
/// Diese Reihenfolge gilt nur fuer VSA-KEK; der WinCan-Import waehlt weiter nach Datum.
/// Die weiteren Untersuchungen gehen nicht verloren, sie werden als Protokollfassung abgelegt.
/// </summary>
internal static class VsaKekUntersuchungsWahl
{
    /// <summary>Was die Wahl von einer Untersuchung wissen muss.</summary>
    internal sealed record Merkmale(bool Abgebrochen, double Laenge, string? Zeitpunkt);

    /// <summary>Die Untersuchungen einer Haltung: die Haupt-Untersuchung und die weiteren.</summary>
    internal sealed record Gruppe<T>(string Haltung, T Haupt, IReadOnlyList<T> Weitere);

    private static readonly string[] Zeitformate =
    {
        "yyyyMMdd", "yyyyMMddHHmmss", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "dd.MM.yyyy"
    };

    /// <summary>
    /// Gruppiert die Haltungsuntersuchungen nach Bezeichnung, verglichen wie die Uebernahme
    /// den Projektdatensatz findet (<see cref="HoldingKeyNormalizer.Normalize"/>, Gross-/
    /// Kleinschreibung egal), und ordnet jede Gruppe nach der Regel oben. Die Gruppen stehen
    /// in der Reihenfolge ihres ersten Auftretens.
    /// </summary>
    public static List<Gruppe<T>> Waehle<T>(
        IEnumerable<T> haltungsuntersuchungen,
        Func<T, string> bezeichnung,
        Func<T, Merkmale> merkmale)
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
                // OrderBy/ThenBy sind stabil: Gleichstand behaelt die Dateireihenfolge.
                var geordnet = jeHaltung[schluessel]
                    .Select(u => (Untersuchung: u, Merkmale: merkmale(u)))
                    .OrderBy(k => k.Merkmale.Abgebrochen)
                    .ThenByDescending(k => k.Merkmale.Laenge)
                    .ThenByDescending(k => UntersuchungsAuswahl.Sortierschluessel(new[] { LiesZeitpunkt(k.Merkmale.Zeitpunkt) }))
                    .Select(k => k.Untersuchung)
                    .ToList();
                return new Gruppe<T>(schluessel, geordnet[0], geordnet.Skip(1).ToList());
            })
            .ToList();
    }

    /// <summary>
    /// Liest den Rohwert von <c>Zeitpunkt</c>; ein unlesbarer Wert ergibt <c>null</c>. Was die
    /// festen Formate schon kannten, liest sich wie bisher (mit Uhrzeit). Seit 01.10.2026 (zweite
    /// Runde) zusaetzlich jedes ISO-Datum, das auch <c>Letzte_Aenderung</c> liest
    /// (<see cref="XtfValueNormalizer.NormalizeDate"/>: Uhrzeit ohne Sekunden, Leerzeichen statt
    /// <c>T</c>, Zone <c>Z</c> oder Versatz) — dann nur der Kalendertag, wie er in der Datei steht.
    /// </summary>
    public static DateTime? LiesZeitpunkt(string? roh)
    {
        var text = (roh ?? "").Trim();
        if (DateTime.TryParseExact(text, Zeitformate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
            return datum;

        return DateTime.TryParseExact(XtfValueNormalizer.NormalizeDate(text), "dd.MM.yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var tag)
            ? tag
            : null;
    }

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
}
