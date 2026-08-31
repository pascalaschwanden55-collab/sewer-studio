using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Eine Haltung, soweit die Strassenuebernahme sie braucht.</summary>
public sealed record StrassenHaltung(
    string Name,
    string? Strasse,
    string? SchachtOben,
    string? SchachtUnten);

/// <summary>Ein Schacht, soweit die Strassenuebernahme ihn braucht.</summary>
public sealed record StrassenSchacht(string Nummer, string? Strasse);

/// <summary>Ein einzelner Uebernahmevorschlag fuer den Stapellauf.</summary>
public sealed record StrassenUebernahmeZeile(string Nummer, string Wert, string Herkunft);

/// <summary>
/// Traegt die Strasse zwischen einer Haltung und ihren beiden Schaechten
/// weiter. Ober- und Unterschacht liegen an derselben Stelle wie die Leitung,
/// also gilt dort dieselbe Adresse.
///
/// Reine Regel ohne Zustand, ohne Dateizugriff und ohne Netz: Sie rechnet nur
/// Vorschlaege aus. Geschrieben wird erst nach ausdruecklicher Bestaetigung,
/// und nur in ein leeres Feld — was importiert oder von Hand gesetzt wurde,
/// bleibt unangetastet.
///
/// Mehrere verschiedene Strassen an einem Schacht werden nicht aufgeloest.
/// In Jagdmatt sind das zwei Faelle ("Linden" gegen "Linden 12"); dort
/// entscheidet der Mensch, nicht die Reihenfolge der Liste.
/// </summary>
public interface IStrassenUebernahme
{
    FeldNachschlagErgebnis FuerSchacht(
        string schachtnummer, IReadOnlyList<StrassenHaltung> haltungen);

    FeldNachschlagErgebnis FuerHaltung(
        StrassenHaltung haltung, IReadOnlyList<StrassenSchacht> schaechte);

    /// <summary>
    /// Alle eindeutigen Vorschlaege fuer leere Schachtfelder. Mehrdeutige
    /// bleiben bewusst draussen — der Stapellauf soll nichts entscheiden,
    /// was der Mensch entscheiden muss.
    /// </summary>
    IReadOnlyList<StrassenUebernahmeZeile> AlleSchaechte(
        IReadOnlyList<StrassenSchacht> schaechte, IReadOnlyList<StrassenHaltung> haltungen);

    /// <summary>Dasselbe in der Gegenrichtung: leere Haltungsfelder aus den Schaechten.</summary>
    IReadOnlyList<StrassenUebernahmeZeile> AlleHaltungen(
        IReadOnlyList<StrassenHaltung> haltungen, IReadOnlyList<StrassenSchacht> schaechte);

    /// <summary>
    /// Schaechte, fuer die es zwar Nachbarn gibt, deren Strassen sich aber
    /// widersprechen. Sie erscheinen in der Vorschau als offene Faelle statt
    /// stillschweigend zu fehlen.
    /// </summary>
    IReadOnlyList<string> MehrdeutigeSchaechte(
        IReadOnlyList<StrassenSchacht> schaechte, IReadOnlyList<StrassenHaltung> haltungen);
}

/// <inheritdoc cref="IStrassenUebernahme"/>
public sealed class StrassenUebernahme : IStrassenUebernahme
{
    /// <summary>Der Herkunftshinweis, den das Uebernehmen auswertet.</summary>
    public const string HerkunftNachbar = "Nachbarbauteil";

    public FeldNachschlagErgebnis FuerSchacht(
        string schachtnummer, IReadOnlyList<StrassenHaltung> haltungen)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        var nummer = Sauber(schachtnummer);
        if (nummer.Length == 0)
            return new FeldNachschlagErgebnis.NichtGefunden("Ohne Schachtnummer keine Uebernahme.");

        var quellen = haltungen
            .Where(h => h is not null)
            .Where(h => Gleich(h.SchachtOben, nummer) || Gleich(h.SchachtUnten, nummer))
            .ToList();

        if (quellen.Count == 0)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                $"Keine Haltung nennt Schacht {nummer} als Ober- oder Unterschacht.");
        }

        return Ergebnis(
            quellen.Select(h => (Wert: Sauber(h.Strasse), Quelle: $"Haltung {Sauber(h.Name)}")),
            $"Die Haltungen an Schacht {nummer} fuehren selbst keine Strasse.");
    }

    public FeldNachschlagErgebnis FuerHaltung(
        StrassenHaltung haltung, IReadOnlyList<StrassenSchacht> schaechte)
    {
        ArgumentNullException.ThrowIfNull(haltung);
        ArgumentNullException.ThrowIfNull(schaechte);

        var knoten = new[] { Sauber(haltung.SchachtOben), Sauber(haltung.SchachtUnten) }
            .Where(k => k.Length > 0)
            .ToList();

        if (knoten.Count == 0)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                $"Die Haltung {Sauber(haltung.Name)} fuehrt keinen Ober- oder Unterschacht.");
        }

        var quellen = schaechte
            .Where(s => s is not null)
            .Where(s => knoten.Any(k => Gleich(s.Nummer, k)))
            .ToList();

        if (quellen.Count == 0)
        {
            return new FeldNachschlagErgebnis.NichtGefunden(
                "Die genannten Schaechte stehen nicht im Projekt.");
        }

        return Ergebnis(
            quellen.Select(s => (Wert: Sauber(s.Strasse), Quelle: $"Schacht {Sauber(s.Nummer)}")),
            "Die Schaechte dieser Haltung fuehren selbst keine Strasse.");
    }

    public IReadOnlyList<StrassenUebernahmeZeile> AlleSchaechte(
        IReadOnlyList<StrassenSchacht> schaechte, IReadOnlyList<StrassenHaltung> haltungen)
    {
        ArgumentNullException.ThrowIfNull(schaechte);
        ArgumentNullException.ThrowIfNull(haltungen);

        return schaechte
            .Where(s => s is not null && Sauber(s.Strasse).Length == 0)
            .Select(s => (s.Nummer, Ergebnis: FuerSchacht(s.Nummer, haltungen)))
            .Where(t => t.Ergebnis is FeldNachschlagErgebnis.Gefunden)
            .Select(t => Zeile(Sauber(t.Nummer), (FeldNachschlagErgebnis.Gefunden)t.Ergebnis))
            .ToList();
    }

    public IReadOnlyList<StrassenUebernahmeZeile> AlleHaltungen(
        IReadOnlyList<StrassenHaltung> haltungen, IReadOnlyList<StrassenSchacht> schaechte)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(schaechte);

        return haltungen
            .Where(h => h is not null && Sauber(h.Strasse).Length == 0)
            .Select(h => (h.Name, Ergebnis: FuerHaltung(h, schaechte)))
            .Where(t => t.Ergebnis is FeldNachschlagErgebnis.Gefunden)
            .Select(t => Zeile(Sauber(t.Name), (FeldNachschlagErgebnis.Gefunden)t.Ergebnis))
            .ToList();
    }

    public IReadOnlyList<string> MehrdeutigeSchaechte(
        IReadOnlyList<StrassenSchacht> schaechte, IReadOnlyList<StrassenHaltung> haltungen)
    {
        ArgumentNullException.ThrowIfNull(schaechte);
        ArgumentNullException.ThrowIfNull(haltungen);

        return schaechte
            .Where(s => s is not null && Sauber(s.Strasse).Length == 0)
            .Where(s => FuerSchacht(s.Nummer, haltungen) is FeldNachschlagErgebnis.Mehrdeutig)
            .Select(s => Sauber(s.Nummer))
            .ToList();
    }

    private static StrassenUebernahmeZeile Zeile(string nummer, FeldNachschlagErgebnis.Gefunden treffer)
        => new(nummer, treffer.Vorschlag.Wert, treffer.Vorschlag.QuelleKlartext);

    /// <summary>
    /// Verschiedene Schreibweisen sind verschiedene Adressen: "Linden" und
    /// "Linden 12" duerfen nicht zu einer verschmelzen. Verglichen wird
    /// deshalb genau, nur Gross-/Kleinschreibung zaehlt nicht.
    /// </summary>
    private static FeldNachschlagErgebnis Ergebnis(
        IEnumerable<(string Wert, string Quelle)> quellen, string grundWennLeer)
    {
        var vorschlaege = quellen
            .Where(q => q.Wert.Length > 0)
            .GroupBy(q => q.Wert, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FeldVorschlag(
                g.First().Wert,
                string.Join(", ", g.Select(q => q.Quelle).Distinct(StringComparer.Ordinal)),
                HerkunftNachbar))
            .ToList();

        if (vorschlaege.Count == 0)
            return new FeldNachschlagErgebnis.NichtGefunden(grundWennLeer);

        return vorschlaege.Count == 1
            ? new FeldNachschlagErgebnis.Gefunden(vorschlaege[0])
            : new FeldNachschlagErgebnis.Mehrdeutig(vorschlaege);
    }

    private static string Sauber(string? wert) => (wert ?? string.Empty).Trim();

    private static bool Gleich(string? a, string b)
        => string.Equals(Sauber(a), b, StringComparison.OrdinalIgnoreCase);
}
