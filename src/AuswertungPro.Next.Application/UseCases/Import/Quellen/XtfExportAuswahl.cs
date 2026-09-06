using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Eine gepruefte XTF-Datei als Kandidat fuer den Import.</summary>
public sealed record XtfExportKandidat(string Pfad, XtfQuellenmerkmale Merkmale);

/// <summary>Warum eine Datei uebernommen oder abgelehnt wurde.</summary>
public sealed record XtfExportEntscheid(string Pfad, bool Uebernommen, string Grund);

/// <summary>Ergebnis der Auswahl: was gelesen wird und warum der Rest wegfaellt.</summary>
public sealed record XtfExportWahl(
    IReadOnlyList<string> Uebernommen,
    IReadOnlyList<XtfExportEntscheid> Entscheide);

/// <summary>
/// Waehlt aus mehreren XTF-Dateien eines Exportordners die Dateien aus, die wirklich
/// gelesen werden sollen.
///
/// Anlass (Audit 2026-09-05, Andermatt Zone 2.11): Unter <c>Misc\Exchange\</c> liegen
/// drei Ordner mit je einer <c>Zone_2.11.xtf</c>. Alle drei enthalten dieselben 48
/// Untersuchungen; nur die erste fuehrt zusaetzlich die 220 Foto- und Videoverweise.
/// Der Import las alle drei und meldete "144 gefunden" — dreimal dieselbe Zone. Die
/// Zahl war damit als Pruefgroesse wertlos.
///
/// Die Untersuchungsmenge gruppiert nur Kandidaten. Übersprungen wird eine Datei
/// ausschliesslich bei belegter XML-Inhaltsteilmenge samt Kennungen und Verweisen.
/// Andere Werte, neue TIDs oder fehlende Inhaltsbelege bleiben sichtbar erhalten.
///
/// Bewusst NICHT nach Aenderungsdatum oder Dateireihenfolge: Beides sagt nichts darueber
/// aus, welcher Export vollstaendiger ist. Bei Gleichstand entscheidet der Pfad
/// alphabetisch, damit zwei Laeufe dasselbe Ergebnis liefern.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class XtfExportAuswahl
{
    /// <param name="auchKataster">
    /// <c>true</c> nimmt auch reine Katasterdateien auf. Sie sind keine Inspektionsquelle
    /// und taugen nicht als Hauptquelle — sie tragen aber die Normschaechte. In Goeschenen
    /// Unterdorfstrasse sind das 71 Schaechte, die sonst niemand sieht (gemessen 2026-09-05).
    /// </param>
    public static XtfExportWahl Waehle(
        IReadOnlyList<XtfExportKandidat> kandidaten,
        bool auchKataster = false)
    {
        ArgumentNullException.ThrowIfNull(kandidaten);

        var entscheide = new List<XtfExportEntscheid>();
        var uebernommen = new List<string>();

        var brauchbar = new List<XtfExportKandidat>();
        foreach (var k in kandidaten)
        {
            if (k.Merkmale.Lesefehler is not null)
            {
                entscheide.Add(new XtfExportEntscheid(k.Pfad, false,
                    $"nicht lesbar — {k.Merkmale.Lesefehler}"));
                continue;
            }

            var art = XtfQuellenklassifikation.Art(k.Merkmale);
            var istVerwertbar = XtfQuellenklassifikation.IstInspektionsquelle(k.Merkmale)
                                || (auchKataster && art == XtfQuellenart.Kataster);
            if (!istVerwertbar)
            {
                entscheide.Add(new XtfExportEntscheid(k.Pfad, false,
                    XtfQuellenklassifikation.Begruendung(k.Merkmale)));
                continue;
            }

            brauchbar.Add(k);
        }

        // Gruppieren nach der Untersuchungsmenge. Ein leerer Fingerabdruck kann nicht
        // zusammengefasst werden — solche Dateien bleiben einzeln stehen.
        foreach (var gruppe in brauchbar.GroupBy(k => k.Merkmale.Untersuchungsfingerabdruck, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(gruppe.Key))
            {
                foreach (var k in gruppe.OrderBy(k => k.Pfad, StringComparer.OrdinalIgnoreCase))
                {
                    uebernommen.Add(k.Pfad);
                    entscheide.Add(new XtfExportEntscheid(k.Pfad, true,
                        XtfQuellenklassifikation.Begruendung(k.Merkmale)));
                }

                continue;
            }

            var sortiert = gruppe
                .OrderByDescending(Reichhaltigkeit)
                .ThenBy(k => k.Pfad, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sieger = sortiert[0];
            uebernommen.Add(sieger.Pfad);
            entscheide.Add(new XtfExportEntscheid(sieger.Pfad, true,
                sortiert.Count == 1
                    ? XtfQuellenklassifikation.Begruendung(sieger.Merkmale)
                    : $"{XtfQuellenklassifikation.Begruendung(sieger.Merkmale)} — "
                      + $"umfangreichster von {sortiert.Count} Exporten derselben Untersuchungsgruppe"));

            foreach (var doppelt in sortiert.Skip(1))
            {
                // Nur wegwerfen, was der Sieger nachweislich vollstaendig enthaelt.
                // Sonst geht Inhalt verloren, den nur die zweite Datei fuehrt — dann
                // wird sie zusaetzlich gelesen und der Widerspruch benannt.
                if (IstTeilmengeVon(doppelt.Merkmale, sieger.Merkmale))
                {
                    entscheide.Add(new XtfExportEntscheid(doppelt.Pfad, false,
                        $"gleiche Untersuchungen wie {Dateiname(sieger.Pfad)} und dort vollstaendig "
                        + "enthalten — nicht nochmals gelesen"));
                    continue;
                }

                uebernommen.Add(doppelt.Pfad);
                entscheide.Add(new XtfExportEntscheid(doppelt.Pfad, true,
                    $"gleiche Untersuchungen wie {Dateiname(sieger.Pfad)}, aber abweichender Inhalt — "
                    + "wird zusaetzlich gelesen und sollte geprueft werden"));
            }
        }

        return new XtfExportWahl(uebernommen, entscheide);
    }

    /// <summary>
    /// Jedes vollständige Objekt muss mindestens ebenso oft im anderen Export stehen.
    /// Eine gleiche Anzahl oder Untersuchungsgruppe ist dafür kein Beweis.
    /// </summary>
    private static bool IstTeilmengeVon(XtfQuellenmerkmale klein, XtfQuellenmerkmale gross)
        => klein.Inhaltsbelege is { Count: > 0 } a
           && gross.Inhaltsbelege is { Count: > 0 } b
           && a.All(p => b.TryGetValue(p.Key, out var anzahl) && anzahl >= p.Value);

    private static int Reichhaltigkeit(XtfExportKandidat k)
        => k.Merkmale.Untersuchungen
           + k.Merkmale.Kanalschaeden
           + k.Merkmale.Normschachtschaeden
           + k.Merkmale.Stammdatenobjekte
           + k.Merkmale.Dateiverweise;

    private static string Dateiname(string pfad)
    {
        var trenner = pfad.LastIndexOfAny(['\\', '/']);
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }
}
