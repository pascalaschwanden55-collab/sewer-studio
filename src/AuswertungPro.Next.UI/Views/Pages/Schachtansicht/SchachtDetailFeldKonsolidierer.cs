using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>Ein zu einem einzigen Detail-Feld konsolidiertes Schacht-Feld.</summary>
public sealed record KonsolidiertesSchachtFeld(
    string AnzeigeName,               // kanonischer Name (kein Mojibake, echte Umlaute wenn vorhanden)
    string PrimaerKey,                // Roh-Feldname, auf den Aenderungen committen
    string Wert,                      // erster nicht-leerer Wert ueber alle Varianten
    IReadOnlyList<string> AlleKeys);  // alle Roh-Varianten dieser Gruppe

/// <summary>
/// Fuehrt Encoding-Varianten desselben Schacht-Feldnamens zusammen: Mojibake „AusfÃ¼hrung",
/// Transliteration „Ausfuehrung" und echte Umlaute „Ausführung" ergeben EIN Feld. Reine Logik
/// (kein WPF), damit sie unit-testbar bleibt. Die Detailansicht nutzt das, um Doppel-Felder
/// zu vermeiden — analog zum festen FieldCatalog der Haltungen.
/// </summary>
public static class SchachtDetailFeldKonsolidierer
{
    /// <summary>
    /// Kanonischer Vergleichsschluessel. WICHTIG: erst kleinschreiben, dann die bereits
    /// kleingeschriebenen Mojibake-/Umlaut-Sequenzen ersetzen (die bestehende Normalize im
    /// Code-behind ersetzt gross geschriebene Sequenzen NACH ToLower und greift darum nie).
    /// </summary>
    public static string Kanonschluessel(string? feldName)
    {
        if (string.IsNullOrWhiteSpace(feldName))
            return "";

        var s = feldName.Trim().ToLowerInvariant()
            // Doppel-Mojibake zuerst (laengste Sequenzen), dann einfaches Mojibake, dann echte Umlaute.
            .Replace("ãƒâ¤", "ae", StringComparison.Ordinal)
            .Replace("ãƒâ¶", "oe", StringComparison.Ordinal)
            .Replace("ãƒâ¼", "ue", StringComparison.Ordinal)
            .Replace("ãƒå¸", "ss", StringComparison.Ordinal)
            .Replace("ã¤", "ae", StringComparison.Ordinal)
            .Replace("ã¶", "oe", StringComparison.Ordinal)
            .Replace("ã¼", "ue", StringComparison.Ordinal)
            .Replace("ãÿ", "ss", StringComparison.Ordinal)
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

        // Mehrfach-Whitespace kollabieren, damit „Datum / Jahr" == „Datum/Jahr" nicht trennt.
        var sb = new StringBuilder(s.Length);
        var lastSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastSpace) sb.Append(' ');
                lastSpace = true;
            }
            else { sb.Append(ch); lastSpace = false; }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Enthaelt der Name eine Mojibake-Sequenz (fehlinterpretiertes UTF-8, erkennbar am „ã").</summary>
    private static bool HatMojibake(string name)
        => name.Contains('Ã') || name.Contains('ã');

    /// <summary>
    /// Konsolidiert Template-Spalten und Record-Felder zu einer duplikatfreien, geordneten Liste.
    /// Reihenfolge: Template-Spalten zuerst (deren Reihenfolge), danach uebrige Felder alphabetisch.
    /// Pro kanonischem Feld: sauberster Anzeigename, erster nicht-leerer Wert, Primaer-Key fuer Commits.
    /// </summary>
    public static IReadOnlyList<KonsolidiertesSchachtFeld> Konsolidiere(
        IEnumerable<string> templateSpalten,
        IReadOnlyDictionary<string, string> recordFelder)
    {
        var gruppen = new Dictionary<string, List<string>>(StringComparer.Ordinal); // kanon -> roh-keys (Reihenfolge)
        var reihenfolge = new List<string>();

        void Erfasse(string? rohKey)
        {
            if (!IstSichtbarerFeldname(rohKey))
                return;
            var key = rohKey!.Trim();
            var kanon = Kanonschluessel(key);
            if (kanon.Length == 0)
                return;
            if (!gruppen.TryGetValue(kanon, out var liste))
            {
                liste = new List<string>();
                gruppen[kanon] = liste;
                reihenfolge.Add(kanon);
            }
            if (!liste.Contains(key, StringComparer.Ordinal))
                liste.Add(key);
        }

        foreach (var t in templateSpalten ?? Enumerable.Empty<string>())
            Erfasse(t);
        foreach (var f in (recordFelder?.Keys ?? Enumerable.Empty<string>())
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Erfasse(f);

        string WertVon(string key)
            => recordFelder is not null && recordFelder.TryGetValue(key, out var v) ? (v ?? "") : "";

        var ergebnis = new List<KonsolidiertesSchachtFeld>(reihenfolge.Count);
        foreach (var kanon in reihenfolge)
        {
            var keys = gruppen[kanon];

            // Anzeigename: bevorzugt ohne Mojibake, sonst der erste (meist die Template-Spalte).
            var anzeige = keys.FirstOrDefault(k => !HatMojibake(k)) ?? keys[0];

            // Wert: erste nicht-leere Variante (Template-Feld ist oft leer, Import-Variante traegt den Wert).
            var primaer = keys.FirstOrDefault(k => !string.IsNullOrWhiteSpace(WertVon(k)));
            var wert = primaer is null ? "" : WertVon(primaer);
            // Commit-Ziel: das wertfuehrende Feld, sonst der Anzeige-Key (kanonisch).
            primaer ??= anzeige;

            ergebnis.Add(new KonsolidiertesSchachtFeld(anzeige, primaer, wert, keys));
        }

        return ergebnis;
    }

    private static bool IstSichtbarerFeldname(string? feldName)
    {
        if (string.IsNullOrWhiteSpace(feldName))
            return false;

        var text = feldName.Trim();
        return !text.All(char.IsDigit);
    }
}
