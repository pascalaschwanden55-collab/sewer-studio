using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Dossiers;

public sealed record DossierTocAttachmentEntry(
    int Number,
    string Title,
    string PageNumber);

/// <summary>
/// Die zusaetzlichen Zeilen des Inhaltsverzeichnisses.
///
/// Die drei Kapitel bleiben Word ueberlassen: es rechnet sie aus den
/// Ueberschriften und kennt ihre Seitenzahlen. Was am Schluss dazukommt —
/// TV-Protokolle, Schachtprotokolle, Plaene — steht dagegen gar nicht im
/// Word-Dokument, sondern liegt als eigene Datei daneben. Word kann diese
/// Zeilen also weder finden noch automatisch mit einer Seitenzahl versehen.
/// SewerStudio schlägt deshalb die nächste Seite vor; der Mensch kann sie je
/// Punkt ändern oder bewusst leeren.
/// </summary>
public static class DossierTocAttachments
{
    /// <summary>
    /// Eine vorangestellte Nummer, die der Mensch aus Gewohnheit mitgetippt
    /// hat: "4. TV-Protokolle" oder "5.Plaene". Sie wird entfernt, damit nicht
    /// "4.\t4. TV-Protokolle" im Dossier steht.
    ///
    /// Ein Punkt ist Pflicht — "3 Plaene" ist eine Menge und keine Nummer.
    /// </summary>
    private static readonly Regex FuehrendeNummer = new(
        @"^\d{1,2}\.\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Die Zeilen als Textblock, fortlaufend ab <paramref name="firstNumber"/>.
    /// Leere Zeilen fallen weg und zaehlen nicht mit, sonst entstuenden
    /// Luecken in der Nummerierung.
    /// </summary>
    public static string Build(IEnumerable<string?>? lines, int firstNumber)
        => Format(BuildEntries(lines, firstNumber));

    /// <summary>
    /// Baut Nummer, Titel und die rechts ausgerichtete Seitenzahl. Fehlt bei
    /// einem alten Dossier der passende Listeneintrag, wird ab
    /// <paramref name="firstPageNumber"/> fortlaufend vorgeschlagen. Eine
    /// vorhandene, aber leere Angabe bleibt dagegen bewusst leer.
    /// </summary>
    public static string Build(
        IEnumerable<string?>? lines,
        IEnumerable<string?>? pageNumbers,
        int firstNumber,
        int firstPageNumber)
        => Format(BuildEntries(lines, pageNumbers, firstNumber, firstPageNumber));

    /// <summary>
    /// Die normalisierten Einträge. Vorschau und Word-Ausgabe verwenden damit
    /// dieselben Nummern und denselben Text, auch wenn eine Nummer mitgetippt
    /// oder eine leere Zeile erfasst wurde.
    /// </summary>
    public static IReadOnlyList<DossierTocAttachmentEntry> BuildEntries(
        IEnumerable<string?>? lines,
        int firstNumber)
        => BuildEntries(lines, pageNumbers: null, firstNumber, firstPageNumber: null);

    public static IReadOnlyList<DossierTocAttachmentEntry> BuildEntries(
        IEnumerable<string?>? lines,
        IEnumerable<string?>? pageNumbers,
        int firstNumber,
        int? firstPageNumber)
    {
        if (lines is null)
            return Array.Empty<DossierTocAttachmentEntry>();

        var pages = pageNumbers?.ToList() ?? new List<string?>();
        var nummer = firstNumber;
        var vorgeschlageneSeite = firstPageNumber;
        var entries = new List<DossierTocAttachmentEntry>();
        var inputIndex = 0;

        foreach (var eintrag in lines)
        {
            var text = FuehrendeNummer.Replace((eintrag ?? string.Empty).Trim(), string.Empty).Trim();
            if (text.Length == 0)
            {
                inputIndex++;
                continue;
            }

            var seite = inputIndex < pages.Count
                ? (pages[inputIndex] ?? string.Empty).Trim()
                : vorgeschlageneSeite?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            entries.Add(new DossierTocAttachmentEntry(nummer, text, seite));
            nummer++;
            inputIndex++;

            if (int.TryParse(
                    seite,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var verwendeteSeite) &&
                verwendeteSeite >= 0)
                vorgeschlageneSeite = verwendeteSeite + 1;
            else if (inputIndex > pages.Count && vorgeschlageneSeite is not null)
                vorgeschlageneSeite++;
        }

        return entries;
    }

    private static string Format(IEnumerable<DossierTocAttachmentEntry> entries)
        => string.Join("\n", entries.Select(entry =>
            string.IsNullOrWhiteSpace(entry.PageNumber)
                ? $"{entry.Number}.\t{entry.Title}"
                : $"{entry.Number}.\t{entry.Title}\t{entry.PageNumber}"));
}
