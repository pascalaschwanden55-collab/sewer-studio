using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Eine Haltung des Projekts samt ihrer WinCan-Laufnummer (<c>H66</c>).</summary>
/// <param name="Haltung">Der Haltungsname, z.B. <c>60248-60247</c>.</param>
/// <param name="Bezeichnung">
/// Die beim Import uebernommene Bezeichnung der Quelle. Null, wenn die Quelle keine
/// gefuehrt hat — dann ist diese Haltung ueber diesen Weg nicht erreichbar.
/// </param>
public sealed record SanierungsprotokollHaltung(string Haltung, string? Bezeichnung);

/// <summary>Ein Ziel der Verteilung samt der Bezeichnung, die dorthin gefuehrt hat.</summary>
public sealed record SanierungsprotokollZiel(string Haltung, string Bezeichnung);

/// <summary>Zuordnung samt der Gruende fuer alles, was NICHT zugeordnet werden konnte.</summary>
public sealed record SanierungsprotokollBefund(
    IReadOnlyList<SanierungsprotokollZiel> Ziele,
    IReadOnlyList<string> Hinweise);

/// <summary>
/// Ordnet ein Sanierungsprotokoll (Dichtheitspruefung, Aushaerteprotokoll) seinen
/// Haltungen zu.
///
/// Anlass (Buerglen 2026-09-09): Diese Protokolle nennen die Haltung nur als
/// WinCan-Laufnummer — im Aushaerteprotokoll steht ueberhaupt kein Schacht, in der
/// Dichtheitspruefung stehen Anfangs- und Endschacht einer ganzen Pruefstrecke
/// (<c>DP H12_H13.pdf</c>: 59435 bis 60191 ueber zwei Haltungen). Das Schachtpaar
/// des bestehenden Verteilers ergibt dort keine Haltung des Projekts, sondern einen
/// neuen erfundenen Ordner.
///
/// Belege, beide zusammen:
///
/// 1. Der Dateiname nennt die Bezeichnungen (<c>DP H12_H13.pdf</c>).
/// 2. Das Dokument selbst nennt dieselbe Bezeichnung.
///
/// Der Dateiname allein reicht ausdruecklich NICHT — das waere eine Namensvermutung,
/// dieselbe Falle wie frueher bei der Gegenbefahrung. Ohne lesbaren Dokumenttext wird
/// deshalb nichts verteilt.
///
/// Eine Bezeichnung ist nur INNERHALB eines Quellprojekts eindeutig (im Bestand
/// Andermatt kam <c>H6</c> in zwei Zonen fuer zwei verschiedene Haltungen vor). Passt
/// sie auf mehrere Haltungen, bekommt keine davon das Protokoll.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class SanierungsprotokollZuordnung
{
    /// <summary>Bezeichnungen der Form <c>H</c> + Zahl, an echten Zeichengrenzen.</summary>
    private static readonly Regex BezeichnungRegex = new(
        @"(?<![A-Za-z0-9])H\d{1,4}(?![0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static SanierungsprotokollBefund Ordne(
        string dateiname,
        string? dokumenttext,
        IReadOnlyList<SanierungsprotokollHaltung> haltungen)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        var hinweise = new List<string>();
        var name = OhneEndung(Dateiname(dateiname ?? ""));

        var bezeichnungen = BezeichnungRegex.Matches(name)
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (bezeichnungen.Count == 0)
        {
            hinweise.Add($"{Dateiname(dateiname ?? "")}: Der Dateiname nennt keine Haltungsbezeichnung.");
            return new SanierungsprotokollBefund([], hinweise);
        }

        if (string.IsNullOrWhiteSpace(dokumenttext))
        {
            hinweise.Add(
                $"{Dateiname(dateiname ?? "")}: Das Dokument ist nicht lesbar — "
                + "der Dateiname allein ordnet keine Haltung zu.");
            return new SanierungsprotokollBefund([], hinweise);
        }

        var imText = BezeichnungRegex.Matches(dokumenttext)
            .Select(m => m.Value.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var ziele = new List<SanierungsprotokollZiel>();
        foreach (var bezeichnung in bezeichnungen)
        {
            if (!imText.Contains(bezeichnung))
            {
                hinweise.Add(
                    $"{Dateiname(dateiname ?? "")}: {bezeichnung} steht nur im Dateinamen, "
                    + "nicht im Dokument.");
                continue;
            }

            var treffer = haltungen
                .Where(h => !string.IsNullOrWhiteSpace(h.Bezeichnung)
                            && string.Equals(h.Bezeichnung!.Trim(), bezeichnung, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Haltung)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (treffer.Count == 1)
            {
                if (!ziele.Any(z => string.Equals(z.Haltung, treffer[0], StringComparison.OrdinalIgnoreCase)))
                    ziele.Add(new SanierungsprotokollZiel(treffer[0], bezeichnung));
                continue;
            }

            hinweise.Add(treffer.Count == 0
                ? $"{Dateiname(dateiname ?? "")}: Zu {bezeichnung} gibt es keine Haltung im Projekt."
                : $"{Dateiname(dateiname ?? "")}: {bezeichnung} ist mehrdeutig "
                  + $"({string.Join(", ", treffer)}).");
        }

        return new SanierungsprotokollBefund(ziele, hinweise);
    }

    private static string Dateiname(string pfad)
    {
        var trenner = pfad.LastIndexOfAny(['\\', '/']);
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }

    private static string OhneEndung(string dateiname)
    {
        var punkt = dateiname.LastIndexOf('.');
        return punkt > 0 ? dateiname[..punkt] : dateiname;
    }
}
