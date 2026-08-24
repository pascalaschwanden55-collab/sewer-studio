using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Fuehrt die Standardthemen eines Gebiets und die Abweichungen eines Dossiers
/// zu genau einer Liste zusammen.
///
/// Die Regel ist bewusst einfach und erklaerbar: das Gebiet gibt Reihenfolge
/// und Standardtext vor, ein gleichnamiges Dossierthema ersetzt dessen Text,
/// und was nur das Dossier kennt, kommt hinten dran. So bleibt eine Aenderung
/// am Gebiet fuer alle Dossiers wirksam, die nichts Eigenes gesetzt haben.
///
/// Reine Logik ohne Dateisystem und ohne Word.
/// </summary>
public static class DossierTopicResolver
{
    /// <summary>
    /// Die fertige Themenliste. Themen ohne Titel entfallen; ein leerer Text
    /// bleibt erhalten, weil die Zeile im Dossier auch leer zum Ausfuellen
    /// stehen darf.
    /// </summary>
    public static IReadOnlyList<DossierTopicRow> Resolve(
        DossierAreaSettings? area,
        DossierDefinition? dossier)
    {
        var gebiet = Bereinigt(area?.Topics);
        var eigene = Bereinigt(dossier?.Topics);

        var ergebnis = new List<DossierTopicRow>();
        var verbraucht = new HashSet<int>();

        foreach (var thema in gebiet)
        {
            var treffer = FindeIndex(eigene, thema.Title, verbraucht);
            if (treffer >= 0)
            {
                verbraucht.Add(treffer);
                ergebnis.Add(new DossierTopicRow
                {
                    Title = thema.Title,
                    Text = eigene[treffer].Text,
                    ColorHex = eigene[treffer].ColorHex,
                    StyleRanges = KopiereFormat(eigene[treffer].StyleRanges)
                });
                continue;
            }

            ergebnis.Add(new DossierTopicRow
            {
                Title = thema.Title,
                Text = thema.Text,
                ColorHex = thema.ColorHex,
                StyleRanges = KopiereFormat(thema.StyleRanges)
            });
        }

        for (var i = 0; i < eigene.Count; i++)
        {
            if (!verbraucht.Contains(i))
            {
                ergebnis.Add(new DossierTopicRow
                {
                    Title = eigene[i].Title,
                    Text = eigene[i].Text,
                    ColorHex = eigene[i].ColorHex,
                    StyleRanges = KopiereFormat(eigene[i].StyleRanges)
                });
            }
        }

        return ergebnis;
    }

    /// <summary>
    /// Der erste noch nicht verbrauchte Eintrag mit diesem Titel. "Noch nicht
    /// verbraucht" ist wichtig: zwei gleichnamige Gebietsthemen duerfen nicht
    /// beide denselben Dossiertext bekommen.
    /// </summary>
    private static int FindeIndex(
        IReadOnlyList<DossierTopicRow> zeilen, string titel, HashSet<int> verbraucht)
    {
        for (var i = 0; i < zeilen.Count; i++)
        {
            if (verbraucht.Contains(i))
                continue;

            if (string.Equals(zeilen[i].Title, titel, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static List<DossierTopicRow> Bereinigt(IEnumerable<DossierTopicRow>? zeilen)
        => (zeilen ?? Enumerable.Empty<DossierTopicRow>())
            .Where(z => z is not null && !string.IsNullOrWhiteSpace(z.Title))
            .Select(z => new DossierTopicRow
            {
                Title = z.Title.Trim(),
                Text = z.Text ?? string.Empty,
                ColorHex = z.ColorHex ?? string.Empty,
                StyleRanges = KopiereFormat(z.StyleRanges)
            })
            .ToList();

    private static List<DossierTextStyleRange> KopiereFormat(
        IEnumerable<DossierTextStyleRange>? ranges)
        => (ranges ?? Enumerable.Empty<DossierTextStyleRange>())
            .Where(r => r is not null)
            .Select(r => new DossierTextStyleRange
            {
                Start = r.Start,
                Length = r.Length,
                ColorHex = r.ColorHex ?? string.Empty,
                Bold = r.Bold,
                Italic = r.Italic,
                Underline = r.Underline
            })
            .ToList();
}
