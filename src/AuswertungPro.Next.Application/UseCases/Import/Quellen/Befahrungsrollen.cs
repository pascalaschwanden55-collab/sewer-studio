using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Welche Rolle eine Videoaufnahme im Bestand einer Haltung hat.</summary>
public enum Befahrungsrolle
{
    /// <summary>Kein ausreichender oder widerspruechlicher Beleg — bleibt offen.</summary>
    Ungeklaert = 0,

    /// <summary>Die Befahrung in Aufnahmerichtung.</summary>
    Hauptbefahrung = 1,

    /// <summary>Die Befahrung aus der Gegenrichtung.</summary>
    Gegenbefahrung = 2,

    /// <summary>Eine weitere Aufnahme: Wiederholung, Reparaturkontrolle oder Teilaufnahme.</summary>
    WeitereAufnahme = 3
}

/// <summary>
/// Die Belege zu EINER Videodatei.
/// </summary>
/// <param name="Pfad">Vollstaendiger Pfad der Datei.</param>
/// <param name="Kamerarichtung">
/// Rohwert aus Datenbank, XTF oder Protokoll — z.B. <c>in_Fliessrichtung</c> oder
/// <c>gegen_Fliessrichtung</c>. Null = keine Angabe. Die Richtung allein ist KEINE Rolle.
/// </param>
/// <param name="Inhaltsschluessel">
/// Aus der Kopien-Erkennung (AP4). Gleicher Wert = bytegleiche Datei = dieselbe Aufnahme.
/// Null = unbekannt; dann wird KEINE Gleichheit unterstellt.
/// </param>
/// <param name="Projektschluessel">
/// Woher die Datei stammt (Projekt, Gemeinde, Exportordner). Weicht er vom Bauwerk ab,
/// wird die Datei nicht zugeordnet — gleiche Haltungsnamen kommen in verschiedenen
/// Gemeinden vor.
/// </param>
public sealed record BefahrungsBeleg(
    string Pfad,
    string? Kamerarichtung = null,
    string? Inhaltsschluessel = null,
    string? Projektschluessel = null,
    bool IstAktiveUntersuchung = false);

/// <summary>Rolle einer Datei samt Begruendung fuer den Importbericht.</summary>
public sealed record Befahrungszuordnung(string Pfad, Befahrungsrolle Rolle, string Grund);

/// <summary>
/// Ordnet die gefundenen Videodateien einer Haltung ihre Rolle zu.
///
/// Anlass (Audit 2026-09-05): Der Import setzte die Gegeninspektion ueber die Suche nach
/// <c>&lt;Haltung&gt;g</c> — eine reine Namensvermutung. "Zweite Datei = Gegeninspektion"
/// ist fachlich falsch: Zwei verschiedene Videos koennen ebenso gut Wiederholung,
/// Reparaturkontrolle oder Teilaufnahmen sein.
///
/// Belege in dieser Reihenfolge:
///
/// 1. Die aktive Untersuchung bindet das Hauptvideo an das angezeigte Protokoll.
/// 2. <b>Dateinamenskonvention</b> — <c>~G</c>, <c>_G</c> oder <c>-g</c> unmittelbar hinter
///    dem Haltungsnamen. Nur diese Formen, und nur am Ende des Namens.
///
/// Was ausdruecklich KEIN Beleg ist:
/// <list type="bullet">
/// <item>Dateireihenfolge, Dateigroesse oder Aenderungszeit.</item>
/// <item>Der umgedrehte Haltungsname (<c>200-100</c> fuer <c>100-200</c>) — es gibt
///       parallele Leitungen, und eine Gegenrichtung ist damit nicht belegt.</item>
/// <item>Derselbe Aufnahmetag.</item>
/// </list>
///
/// Widerspruechliche Belege ergeben <see cref="Befahrungsrolle.Ungeklaert"/>. Zwei
/// Kandidaten fuer dieselbe Rolle ebenfalls: Dann muss der Mensch entscheiden.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class Befahrungsrollen
{
    /// <param name="haltung">Der Haltungsname, z.B. <c>100-200</c>.</param>
    /// <param name="projektschluessel">
    /// Schluessel des Bauwerks. Null = keine Pruefung (Bestandsaufrufer ohne Projektbezug).
    /// </param>
    public static IReadOnlyList<Befahrungszuordnung> Ordne(
        string haltung,
        IReadOnlyList<BefahrungsBeleg> belege,
        string? projektschluessel = null)
    {
        ArgumentNullException.ThrowIfNull(belege);

        var eindeutig = belege
            .GroupBy(b => b.Pfad, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(b => b.IstAktiveUntersuchung).First())
            .OrderBy(b => b.Pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ergebnis = new List<Befahrungszuordnung>();

        // Fremdes Projekt: gleiche Haltungsnamen kommen in verschiedenen Gemeinden vor.
        var eigene = new List<BefahrungsBeleg>();
        foreach (var beleg in eindeutig)
        {
            if (projektschluessel is not null
                && beleg.Projektschluessel is not null
                && !string.Equals(beleg.Projektschluessel, projektschluessel, StringComparison.OrdinalIgnoreCase))
            {
                ergebnis.Add(new Befahrungszuordnung(beleg.Pfad, Befahrungsrolle.Ungeklaert,
                    $"stammt aus einem anderen Projekt ({beleg.Projektschluessel})"));
                continue;
            }

            eigene.Add(beleg);
        }

        // Bytegleiche Dateien sind EINE Aufnahme. Nur der erste Pfad wird bewertet;
        // die weiteren erben die Rolle als Kopie.
        var kopienJeAufnahme = new Dictionary<string, List<BefahrungsBeleg>>(StringComparer.Ordinal);
        var vertreter = new List<BefahrungsBeleg>();
        foreach (var beleg in eigene.OrderByDescending(b => b.IstAktiveUntersuchung))
        {
            var schluessel = beleg.Inhaltsschluessel;
            if (schluessel is null)
            {
                vertreter.Add(beleg);
                continue;
            }

            if (kopienJeAufnahme.TryGetValue(schluessel, out var kopien))
            {
                kopien.Add(beleg);
                continue;
            }

            kopienJeAufnahme[schluessel] = new List<BefahrungsBeleg> { beleg };
            vertreter.Add(beleg);
        }

        // Erste Einschaetzung je Aufnahme.
        var vorlaeufig = vertreter
            .Select(b => (Beleg: b, Ergebnis: Beurteile(haltung, b)))
            .ToList();

        var aktive = vorlaeufig.Where(v => v.Beleg.IstAktiveUntersuchung).ToList();
        if (aktive.Count == 1)
        {
            var aktiv = aktive[0];
            for (var i = 0; i < vorlaeufig.Count; i++)
            {
                var v = vorlaeufig[i];
                if (v == aktiv)
                    vorlaeufig[i] = (v.Beleg, new(v.Beleg.Pfad, Befahrungsrolle.Hauptbefahrung, "Video der aktiven Untersuchung"));
                else if (aktiv.Ergebnis.Rolle == Befahrungsrolle.Gegenbefahrung
                         && v.Ergebnis.Rolle == Befahrungsrolle.Hauptbefahrung)
                    vorlaeufig[i] = (v.Beleg, new(v.Beleg.Pfad, Befahrungsrolle.Gegenbefahrung, "Benanntes Gegenpaar zur aktiven Untersuchung"));
                else if (v.Ergebnis.Rolle == Befahrungsrolle.Hauptbefahrung)
                    vorlaeufig[i] = (v.Beleg, new(v.Beleg.Pfad, Befahrungsrolle.WeitereAufnahme, "Weitere Untersuchung, kein belegtes Gegenpaar"));
            }
        }

        // Ein benanntes Paar darf auch upstream beginnen. Nur GLEICHE bekannte
        // Richtungen widersprechen einem Gegenpaar, nie die Richtung für sich.
        var haupt = vorlaeufig.Where(v => v.Ergebnis.Rolle == Befahrungsrolle.Hauptbefahrung).ToList();
        if (haupt.Count == 1 && Richtung(haupt[0].Beleg.Kamerarichtung) != 0)
        {
            for (var i = 0; i < vorlaeufig.Count; i++)
            {
                var v = vorlaeufig[i];
                if (v.Ergebnis.Rolle == Befahrungsrolle.Gegenbefahrung
                    && Richtung(v.Beleg.Kamerarichtung) == Richtung(haupt[0].Beleg.Kamerarichtung))
                    vorlaeufig[i] = (v.Beleg, new(v.Beleg.Pfad, Befahrungsrolle.Ungeklaert,
                        "Widerspruch: benanntes Gegenpaar hat dieselbe Kamerarichtung"));
            }
        }

        // Zwei Kandidaten fuer dieselbe Rolle: nicht raten.
        var hauptbefahrungen = vorlaeufig.Where(v => v.Ergebnis.Rolle == Befahrungsrolle.Hauptbefahrung).ToList();
        var gegenbefahrungen = vorlaeufig.Where(v => v.Ergebnis.Rolle == Befahrungsrolle.Gegenbefahrung).ToList();

        if (gegenbefahrungen.Count > 1)
        {
            foreach (var v in gegenbefahrungen)
            {
                vorlaeufig[vorlaeufig.IndexOf(v)] = (v.Beleg, new Befahrungszuordnung(
                    v.Beleg.Pfad, Befahrungsrolle.Ungeklaert,
                    $"{gegenbefahrungen.Count} Kandidaten fuer die Gegenbefahrung — nicht entscheidbar"));
            }
        }

        if (hauptbefahrungen.Count > 1)
        {
            foreach (var v in hauptbefahrungen)
            {
                vorlaeufig[vorlaeufig.IndexOf(v)] = (v.Beleg, new Befahrungszuordnung(
                    v.Beleg.Pfad, Befahrungsrolle.WeitereAufnahme,
                    $"{hauptbefahrungen.Count} Aufnahmen ohne unterscheidenden Beleg — "
                          + "keine davon gilt als Gegenbefahrung"));
            }
        }

        foreach (var (beleg, zuordnung) in vorlaeufig)
        {
            ergebnis.Add(zuordnung);

            if (beleg.Inhaltsschluessel is null
                || !kopienJeAufnahme.TryGetValue(beleg.Inhaltsschluessel, out var kopien))
            {
                continue;
            }

            foreach (var kopie in kopien.Skip(1))
            {
                ergebnis.Add(new Befahrungszuordnung(
                    kopie.Pfad,
                    zuordnung.Rolle,
                    $"bytegleiche Kopie derselben Aufnahme ({zuordnung.Grund})"));
            }
        }

        return ergebnis
            .OrderBy(z => z.Pfad, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Befahrungszuordnung Beurteile(string haltung, BefahrungsBeleg beleg)
    {
        var name = Dateiname(beleg.Pfad);
        var nameSagtGegen = TraegtGegenmarke(name, haltung);

        if (nameSagtGegen)
            return new Befahrungszuordnung(beleg.Pfad, Befahrungsrolle.Gegenbefahrung, "Dateiname mit Gegenmarke");

        if (TraegtHauptmarke(name, haltung))
            return new Befahrungszuordnung(beleg.Pfad, Befahrungsrolle.Hauptbefahrung, "Dateiname nennt die Haltung");

        return new Befahrungszuordnung(beleg.Pfad, Befahrungsrolle.Ungeklaert,
            "keine belegte Rolle: Kamerarichtung allein genügt nicht");
    }

    private static int Richtung(string? wert) => IstGegenrichtung(wert ?? "") ? -1 : IstFliessrichtung(wert) ? 1 : 0;

    private static bool IstGegenrichtung(string richtung)
        => richtung.Replace("_", "", StringComparison.Ordinal)
                   .StartsWith("gegen", StringComparison.OrdinalIgnoreCase);

    private static bool IstFliessrichtung(string? richtung)
    {
        var text = (richtung ?? "").Trim().Replace("_", "", StringComparison.Ordinal);
        return text.StartsWith("in", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("mit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Traegt der Dateiname unmittelbar hinter dem Haltungsnamen eine Gegenmarke?
    /// Zugelassen sind genau <c>~G</c>, <c>_G</c>, <c>-G</c> und ein angehaengtes <c>G</c>.
    /// </summary>
    private static bool TraegtGegenmarke(string dateiname, string haltung)
    {
        var rest = RestHinterHaltung(dateiname, haltung);
        if (rest is null)
            return false;

        rest = rest.TrimStart('~', '_', '-');
        return rest.Length > 0
               && char.ToUpperInvariant(rest[0]) == 'G'
               && rest[1..].All(c => !char.IsLetter(c));
    }

    /// <summary>Nennt der Name die Haltung ohne weitere Buchstaben dahinter?</summary>
    private static bool TraegtHauptmarke(string dateiname, string haltung)
    {
        var rest = RestHinterHaltung(dateiname, haltung);
        return rest is not null && !rest.Any(char.IsLetter);
    }

    private static string? RestHinterHaltung(string dateiname, string haltung)
    {
        var ohneEndung = OhneEndung(dateiname);
        var name = (haltung ?? "").Trim();
        if (name.Length == 0)
            return null;

        var stelle = ohneEndung.IndexOf(name, StringComparison.OrdinalIgnoreCase);
        if (stelle < 0 || (stelle > 0 && char.IsLetterOrDigit(ohneEndung[stelle - 1])))
            return null;
        var rest = ohneEndung[(stelle + name.Length)..];
        if (rest.Length > 0 && char.IsDigit(rest[0])) return null;
        return rest;
    }

    private static string OhneEndung(string dateiname)
    {
        var punkt = dateiname.LastIndexOf('.');
        return punkt > 0 ? dateiname[..punkt] : dateiname;
    }

    private static string Dateiname(string pfad)
    {
        var trenner = pfad.LastIndexOfAny(['\\', '/']);
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }
}
