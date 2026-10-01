using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Ein auswählbarer Seiteneintrag der Vorschau. Das Kapitel ist getrennt von
/// der Seite, damit die Oberfläche beides hierarchisch anzeigen kann.
/// </summary>
public sealed record DossierPreviewNavigationItem(
    string ChapterTitle,
    string PageLabel,
    DossierPreviewPage Page);

/// <summary>
/// Ordnet die Seiten ihren Kapiteln zu. Eine Seite mit Kapitelüberschrift
/// eröffnet eine neue Gruppe; reine Fortsetzungsseiten bleiben im zuletzt
/// begonnenen Kapitel. Deckblatt und Inhaltsverzeichnis bilden vor dem ersten
/// Fachkapitel je eine eigene Gruppe.
/// </summary>
public static class DossierPreviewNavigation
{
    public static IReadOnlyList<DossierPreviewNavigationItem> Build(
        IEnumerable<DossierPreviewPage>? pages)
    {
        if (pages is null)
            return Array.Empty<DossierPreviewNavigationItem>();

        var result = new List<DossierPreviewNavigationItem>();
        var currentChapter = string.Empty;
        var fachkapitelBegonnen = false;

        foreach (var page in pages)
        {
            if (page is null)
                continue;

            var heading = Heading(page);
            if (heading.Length > 0)
            {
                currentChapter = heading;
                fachkapitelBegonnen = true;
            }
            else if (!fachkapitelBegonnen && !IsGenericPageTitle(page))
            {
                currentChapter = page.Title.Trim();
            }

            if (currentChapter.Length == 0)
                currentChapter = IsGenericPageTitle(page) ? "Dossier" : page.Title.Trim();

            result.Add(new DossierPreviewNavigationItem(
                currentChapter,
                $"Seite {page.Number}",
                page));
        }

        return result;
    }

    private static string Heading(DossierPreviewPage page)
        => page.Blocks
            .OfType<DossierPreviewParagraph>()
            .Where(paragraph => paragraph.Format.IsHeading)
            .Select(PlainText)
            .FirstOrDefault(text => text.Length > 0)
            ?? string.Empty;

    private static string PlainText(DossierPreviewParagraph paragraph)
        => string.Concat(paragraph.Runs
                .Where(run => !run.IsField)
                .Select(run => run.Text))
            .Trim();

    private static bool IsGenericPageTitle(DossierPreviewPage page)
        => string.IsNullOrWhiteSpace(page.Title)
            || string.Equals(
                page.Title.Trim(),
                $"Seite {page.Number}",
                StringComparison.OrdinalIgnoreCase);
}
