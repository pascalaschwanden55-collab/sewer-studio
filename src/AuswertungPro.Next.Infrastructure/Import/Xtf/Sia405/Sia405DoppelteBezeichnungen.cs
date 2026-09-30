using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>
/// Zwischen Schritt 2 (Bezuege) und Schritt 3 (Abbildung) des SIA405-Haltungsimports:
/// Zwei Haltungen mit gleicher Bezeichnung, aber verschiedener TID sind zwei
/// Katasterobjekte. Im Projekt gibt es je Bezeichnung nur einen Datensatz.
///
/// Bis 30.09.2026 landeten beide still in diesem einen Datensatz: Die zweite
/// ueberschrieb Objekt_ID, DN, Material und Eigentuemer, andere Felder blieben von der
/// ersten. Jetzt gilt: Die erste Haltung in Dateireihenfolge wird wie bisher
/// uebernommen, jede weitere gar nicht, und der Importbericht nennt sie mit beiden TIDs.
/// </summary>
internal static class Sia405DoppelteBezeichnungen
{
    /// <summary>
    /// Behaelt je Bezeichnung die erste Haltung. Verglichen wird genau so, wie die
    /// Uebernahme den Projektdatensatz findet: <see cref="HoldingKeyNormalizer.Normalize"/>,
    /// Gross-/Kleinschreibung egal. Gleiche TID zweimal bleibt wie bisher erhalten.
    /// </summary>
    public static List<Sia405HaltungMitBezuegen> NurErste(
        IReadOnlyList<Sia405HaltungMitBezuegen> haltungen,
        out List<string> meldungen)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        meldungen = new List<string>();
        var ersteJeSchluessel = new Dictionary<string, Sia405HaltungMitBezuegen>(StringComparer.OrdinalIgnoreCase);
        var ergebnis = new List<Sia405HaltungMitBezuegen>(haltungen.Count);

        foreach (var haltung in haltungen)
        {
            var schluessel = HoldingKeyNormalizer.Normalize(haltung.Haltungsname);
            if (schluessel.Length == 0 || !ersteJeSchluessel.TryGetValue(schluessel, out var erste))
            {
                if (schluessel.Length > 0)
                    ersteJeSchluessel[schluessel] = haltung;
                ergebnis.Add(haltung);
                continue;
            }

            if (string.Equals(erste.Haltung.Tid, haltung.Haltung.Tid, StringComparison.Ordinal))
            {
                ergebnis.Add(haltung);
                continue;
            }

            meldungen.Add(
                $"Haltung '{erste.Haltungsname}' kommt zweimal vor (TID {erste.Haltung.Tid}, "
                + $"TID {haltung.Haltung.Tid}) – nur die erste übernommen.");
        }

        return ergebnis;
    }
}
