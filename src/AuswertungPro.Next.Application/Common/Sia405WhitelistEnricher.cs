using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Ergebnis einer kontrollierten SIA405-Anreicherung.
/// </summary>
/// <param name="Filled">Anzahl neu gesetzter Felder.</param>
/// <param name="Conflicts">Konflikte (Feld hatte Wert, der von SIA405 abweicht).</param>
public sealed record EnrichmentResult(int Filled, IReadOnlyList<string> Conflicts);

/// <summary>
/// Reichert Haltungs-Stammdaten kontrolliert aus dem SIA405-XTF an.
/// Regeln:
///   - Nur Whitelist-Felder werden beruecksichtigt.
///   - Ein Feld wird nur gesetzt, wenn es leer ist (und nicht userEdited).
///   - Bereits gefuellte Werte werden NIEMALS ueberschrieben; Abweichungen werden geloggt.
///   - Protected-Felder werden in der Whitelist bewusst NICHT aufgenommen.
/// Hinweis: Ein "Geometrie"-Feld existiert im aktuellen HaltungRecord-Schema nicht
/// (verifiziert anhand FieldCatalog.ColumnOrder); es wird daher nicht in der Whitelist gefuehrt.
/// </summary>
public static class Sia405WhitelistEnricher
{
    /// <summary>
    /// Felder, die aus dem SIA405-XTF angereichert werden duerfen (nur wenn leer).
    /// </summary>
    public static readonly string[] Whitelist =
    {
        "Rohrmaterial",
        "DN_mm",
        "Nutzungsart",
        "Strasse",
        // Zusaetzliche sichere Stammdaten, die die SIA405-XTF liefert, die VSA_KEK (IKAS) aber nicht
        // setzt — damit das Datagrid maximal gefuellt wird. Weiterhin nur empty-only + konfliktgeloggt,
        // nie ueberschreibend. NICHT aufgenommen: Datum_Jahr/Bemerkungen/Haltungslaenge_m (Protected)
        // und Offen_abgeschlossen (semantisch mehrdeutig: Betriebs- vs. Bearbeitungsstatus).
        "Eigentuemer",
        "Schacht_oben",
        "Schacht_unten"
    };

    /// <summary>
    /// Explizit geschuetzte Felder — nicht in der Whitelist, nie aus SIA405 setzen.
    /// </summary>
    public static readonly string[] Protected =
    {
        "Datum_Jahr",
        "Bemerkungen",
        "Haltungslaenge_m"
    };

    /// <summary>
    /// Wendet die SIA405-Anreicherung auf alle Haltungen im Projekt an.
    /// </summary>
    /// <param name="project">Projekt mit den Haltungs-Records.</param>
    /// <param name="sia405ByHaltung">
    /// Haltungsname (case-insensitiv) -> (Feldname -> Wert) aus dem SIA405-XTF.
    /// </param>
    /// <returns>Anzahl gefuellter Felder und Liste der Konflikt-Meldungen.</returns>
    public static EnrichmentResult Apply(
        Project project,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sia405ByHaltung)
    {
        // Internen case-insensitiven Lookup aufbauen, weil der Aufrufer moeglicherweise
        // einen Default-Comparer (Ordinal/case-sensitiv) verwendet hat. So sind stille
        // Fehltreffer ausgeschlossen, unabhaengig davon, wie die Map gebaut wurde.
        // TryAdd statt Indexer: bei seltenen Case-Duplikaten im Eingabe-Dictionary
        // gewinnt der erste Eintrag und es wird kein ArgumentException geworfen.
        var ci = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in sia405ByHaltung)
            ci.TryAdd(kv.Key, kv.Value);

        var filled = 0;
        var conflicts = new List<string>();

        foreach (var record in project.Data)
        {
            var haltungsname = record.GetFieldValue("Haltungsname");

            // Kein SIA405-Eintrag fuer diese Haltung -> ueberspringen
            if (!ci.TryGetValue(haltungsname, out var sia405Felder))
                continue;

            foreach (var feld in Whitelist)
            {
                // Kein Wert in den SIA405-Daten fuer dieses Feld -> ueberspringen
                if (!sia405Felder.TryGetValue(feld, out var sia405Wert)
                    || string.IsNullOrEmpty(sia405Wert))
                    continue;

                // UserEdited-Felder werden komplett uebersprungen (kein Conflict-Log, kein Fill).
                // SetFieldValue wuerde den Aufruf stil ignorieren, aber wir wollen auch keinen
                // Konflikt-Eintrag erzeugen, da der Benutzer das Feld bewusst gesetzt hat.
                if (record.FieldMeta.TryGetValue(feld, out var meta) && meta.UserEdited)
                    continue;

                var vorhandenerWert = record.GetFieldValue(feld);

                if (string.IsNullOrEmpty(vorhandenerWert))
                {
                    // Feld ist leer und nicht userEdited: aus SIA405 fuellen
                    record.SetFieldValue(feld, sia405Wert, FieldSource.Xtf405, userEdited: false);
                    filled++;
                }
                else if (!string.Equals(vorhandenerWert, sia405Wert, StringComparison.Ordinal))
                {
                    // Abweichung: Konflikt melden, Wert NICHT ueberschreiben
                    conflicts.Add(
                        $"Konflikt Haltung {haltungsname} Feld {feld}: " +
                        $"vorhanden '{vorhandenerWert}' vs SIA405 '{sia405Wert}' — nicht ueberschrieben");
                }
                // Gleicher Wert -> nichts tun
            }
        }

        return new EnrichmentResult(filled, conflicts);
    }
}
