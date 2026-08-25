using System;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Entfernt den einen überflüssigen Umbruch der ausgelieferten Dossier-Vorlage.
/// Der Deckblattinhalt füllt Seite 1 bereits vollständig. Der Absatz
/// "Änderungswesen" beginnt deshalb von selbst auf Seite 2; sein zusätzlicher
/// manueller Umbruch erzeugt dort eine leere Seite.
/// </summary>
internal static class DocxKnownBlankPageRemover
{
    private const string AnchorText = "Änderungswesen:";

    public static bool Apply(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var body = document.MainDocumentPart?.Document?.Body;
        var paragraph = body?.Descendants<Paragraph>()
            .FirstOrDefault(item => string.Equals(
                OwnText(item).Trim(),
                AnchorText,
                StringComparison.Ordinal));
        if (paragraph is null)
            return false;

        var pageBreak = paragraph.Descendants<Break>()
            .FirstOrDefault(item =>
                item.Type?.Value == BreakValues.Page
                && ReferenceEquals(item.Ancestors<Paragraph>().FirstOrDefault(), paragraph));
        if (pageBreak is null)
            return false;

        var run = pageBreak.Parent as Run;
        pageBreak.Remove();

        // Der Umbruch besitzt in der Vorlage einen eigenen, sonst leeren Run.
        // Auch diesen leeren Rest entfernen, damit keine unsichtbare Textstelle
        // zwischen Deckblatt und Änderungswesen übrig bleibt.
        if (run is not null
            && run.ChildElements.All(child => child is RunProperties))
        {
            run.Remove();
        }

        return true;
    }

    private static string OwnText(Paragraph paragraph)
        => string.Concat(paragraph.Descendants<Text>()
            .Where(text => ReferenceEquals(
                text.Ancestors<Paragraph>().FirstOrDefault(),
                paragraph))
            .Select(text => text.Text));
}
