using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ein Dateikandidat mit dem Schluessel seines INHALTS.
/// </summary>
/// <param name="Pfad">Vollstaendiger Pfad.</param>
/// <param name="Inhaltsschluessel">
/// Gleicher Wert = nachweislich gleicher Inhalt. Null, wenn die Datei nicht gelesen
/// werden konnte.
/// </param>
/// <param name="Fehler">Grund, warum der Inhalt nicht bestimmbar war.</param>
public sealed record MedienKandidat(string Pfad, string? Inhaltsschluessel, string? Fehler = null);

/// <summary>Welche Datei gilt — und warum die anderen nicht.</summary>
/// <param name="Pfad">Der zu verwendende Pfad, oder null bei echter Mehrdeutigkeit.</param>
/// <param name="Herkunftspfade">Alle Pfade mit demselben Inhalt, alphabetisch.</param>
/// <param name="Mehrdeutig">Mehrere verschiedene Inhalte — der Mensch muss entscheiden.</param>
/// <param name="Grund">Klartext fuer den Importbericht.</param>
public sealed record MedienWahl(
    string? Pfad,
    IReadOnlyList<string> Herkunftspfade,
    bool Mehrdeutig,
    string Grund);

/// <summary>
/// Bestimmt den Inhaltsschluessel mehrerer Dateien. Die Umsetzung darf zwischenspeichern
/// und soll zuerst nach Groesse eingrenzen — nur Dateien gleicher Groesse muessen
/// wirklich vollstaendig verglichen werden.
/// </summary>
public interface IMedienInhaltsIndex
{
    IReadOnlyList<MedienKandidat> Pruefe(IReadOnlyList<string> pfade);
}

/// <summary>
/// Waehlt aus mehreren gefundenen Dateien die eine aus, die verwendet wird.
///
/// Anlass (Audit 2026-09-05, Andermatt Zone 2.11): Fuer die Haltung 327015-2414 liegt
/// dasselbe Video zweimal im Ordner — einmal unter <c>Video\Sec</c> und einmal im
/// XTF-Exportordner. Beide Dateien sind bytegleich. Der Import zaehlte "mehrere
/// Kandidaten" und verlinkte deshalb GAR KEIN Video. Zwei Kopien sind aber nur EINE
/// Aufnahme.
///
/// Umgekehrt gilt: Zwei verschiedene Dateien bleiben zwei Kandidaten, auch wenn sie
/// gleich gross sind. Und eine unlesbare Datei darf nie als Kopie unterstellt werden —
/// dann bleibt der Fall offen.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class MedienKandidatenAuswahl
{
    public static MedienWahl Waehle(IReadOnlyList<MedienKandidat> kandidaten)
    {
        ArgumentNullException.ThrowIfNull(kandidaten);

        // Derselbe Pfad kann aus zwei Suchwegen kommen; das ist keine zweite Datei.
        var eindeutig = kandidaten
            .GroupBy(k => k.Pfad, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(k => k.Pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (eindeutig.Count == 0)
            return new MedienWahl(null, Array.Empty<string>(), false, "keine Datei gefunden");

        var unlesbar = eindeutig.Where(k => k.Inhaltsschluessel is null).ToList();
        var lesbar = eindeutig.Where(k => k.Inhaltsschluessel is not null).ToList();

        if (lesbar.Count == 0)
        {
            return new MedienWahl(null, Array.Empty<string>(), false,
                $"gefunden, aber nicht lesbar: {Beschreibe(unlesbar)}");
        }

        // Eine unlesbare Datei neben lesbaren: Ob sie dasselbe enthaelt, ist NICHT
        // pruefbar. Eine Kopie zu unterstellen waere geraten.
        if (unlesbar.Count > 0)
        {
            return new MedienWahl(null, Array.Empty<string>(), true,
                $"{eindeutig.Count} Kandidaten, davon nicht lesbar: {Beschreibe(unlesbar)} — "
                + "Gleichheit nicht pruefbar");
        }

        var gruppen = lesbar
            .GroupBy(k => k.Inhaltsschluessel!, StringComparer.Ordinal)
            .ToList();

        if (gruppen.Count > 1)
        {
            return new MedienWahl(null, Array.Empty<string>(), true,
                $"{eindeutig.Count} Kandidaten mit {gruppen.Count} verschiedenen Inhalten — "
                + "die richtige Datei muss von Hand bestimmt werden");
        }

        var herkunft = gruppen[0]
            .Select(k => k.Pfad)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MedienWahl(
            herkunft[0],
            herkunft,
            false,
            herkunft.Count == 1
                ? "eindeutig"
                : $"{herkunft.Count} bytegleiche Kopien — dieselbe Aufnahme");
    }

    private static string Beschreibe(IEnumerable<MedienKandidat> kandidaten)
        => string.Join("; ", kandidaten.Select(k => $"{Dateiname(k.Pfad)} ({k.Fehler})"));

    private static string Dateiname(string pfad)
    {
        var trenner = pfad.LastIndexOfAny(['\\', '/']);
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }
}
