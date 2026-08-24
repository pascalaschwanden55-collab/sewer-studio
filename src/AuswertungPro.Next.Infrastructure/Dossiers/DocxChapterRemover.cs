using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Nimmt ein ganzes Kapitel aus dem Dokument.
///
/// Wer keinen Uebersichtsplan hat, soll keine leere Seite verschicken. Entfernt
/// wird deshalb nicht nur die Bildstelle, sondern die Ueberschrift, alles bis
/// zum naechsten Kapitel und die zugehoerige Zeile im Inhaltsverzeichnis.
///
/// Erkannt wird das Kapitel an seiner Ueberschrift. Findet sich keine, wird
/// NICHTS entfernt: lieber ein Kapitel zu viel als ein halb abgeraeumtes
/// Dokument.
/// </summary>
public static class DocxChapterRemover
{
    public static bool Remove(WordprocessingDocument document, string? headingText)
    {
        ArgumentNullException.ThrowIfNull(document);

        var titel = (headingText ?? string.Empty).Trim();
        if (titel.Length == 0)
            return false;

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return false;

        var kinder = body.ChildElements.ToList();
        var start = kinder.FindIndex(e => e is Paragraph p && IstUeberschrift(p, titel));

        if (start < 0)
            return false;

        // Bis zur naechsten Ueberschrift — oder bis zum Ende des Textkoerpers.
        var ende = kinder.FindIndex(start + 1, e => e is Paragraph p && IstUeberschrift(p));
        if (ende < 0)
            ende = kinder.Count;

        for (var i = ende - 1; i >= start; i--)
        {
            if (kinder[i] is SectionProperties)
                continue;

            kinder[i].Remove();
        }

        EntferneVerzeichniszeile(body, titel);
        return true;
    }

    /// <summary>
    /// Die Zeile im Inhaltsverzeichnis. Sie traegt eine Nummer vor dem Titel,
    /// deshalb wird auf "enthaelt" geprueft — und nur eine Zeile im
    /// Verzeichnisformat kommt in Frage, damit nicht irgendein Fliesstext mit
    /// demselben Wort verschwindet.
    /// </summary>
    private static void EntferneVerzeichniszeile(Body body, string titel)
    {
        foreach (var absatz in body.Elements<Paragraph>().ToList())
        {
            var stil = absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;

            var imVerzeichnis = stil.StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase)
                || stil.StartsWith("TOC", StringComparison.OrdinalIgnoreCase);

            if (!imVerzeichnis)
                continue;

            if (Text(absatz).Contains(titel, StringComparison.OrdinalIgnoreCase))
                absatz.Remove();
        }
    }

    private static bool IstUeberschrift(Paragraph absatz, string? titel = null)
    {
        var stil = absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;

        var ueberschrift =
            stil.StartsWith("berschrift", StringComparison.OrdinalIgnoreCase)
            || stil.StartsWith("Überschrift", StringComparison.OrdinalIgnoreCase)
            || stil.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);

        if (!ueberschrift)
            return false;

        return titel is null
            || string.Equals(Text(absatz).Trim(), titel, StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(OpenXmlElement element)
        => string.Concat(element.Descendants<Text>().Select(t => t.Text));

    /// <summary>Die Ueberschriften des Dokuments, in ihrer Reihenfolge.</summary>
    public static IReadOnlyList<string> Chapters(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return Array.Empty<string>();

        return body.Elements<Paragraph>()
            .Where(p => IstUeberschrift(p))
            .Select(p => Text(p).Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }
}
