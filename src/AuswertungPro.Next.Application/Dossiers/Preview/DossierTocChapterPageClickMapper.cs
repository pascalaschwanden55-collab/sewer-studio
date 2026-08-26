using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Verbindet die rechts ausgerichtete Seitenzahl einer festen
/// Inhaltsverzeichniszeile mit ihrem eigenen Eingabefeld.
///
/// Die Zahl wird bewusst nicht global gesucht: Zwei Kapitel duerfen beide auf
/// Seite 4 beginnen. Massgeblich ist die Zahl rechts in derselben PDF-Zeile wie
/// der bereits eindeutig erkannte Kapiteltitel.
/// </summary>
public static class DossierTocChapterPageClickMapper
{
    private const string TargetKeyPrefix = "Verzeichnis_Kapitel:";
    public const string PageCellKey = "Seite";

    public static DossierPreviewTarget PageTarget(string originalTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalTitle);
        return DossierPreviewTarget.RowCell(
            TargetKeyPrefix + originalTitle,
            0,
            PageCellKey);
    }

    public static bool IsPageTarget(DossierPreviewTarget target)
        => target.Kind is DossierPreviewTargetKind.RowCell
            && target.Key.StartsWith(TargetKeyPrefix, StringComparison.Ordinal)
            && string.Equals(target.CellKey, PageCellKey, StringComparison.Ordinal);

    public static string? OriginalTitle(DossierPreviewTarget target)
        => IsPageTarget(target)
            ? target.Key[TargetKeyPrefix.Length..]
            : null;

    public static IReadOnlyList<string> ChapterTitles(
        IEnumerable<DossierPreviewPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        return pages
            .SelectMany(page => page.Blocks)
            .OfType<DossierPreviewParagraph>()
            .Select(paragraph => paragraph.TocEntry?.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title!)
            .ToList();
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> AddPageTargets(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        IReadOnlyList<string> chapterTitles)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(chapterTitles);

        var result = hits.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Distinct().ToList());

        for (var rowIndex = 0; rowIndex < chapterTitles.Count; rowIndex++)
        {
            var titleTarget = DossierPreviewTarget.Literal(chapterTitles[rowIndex]);
            var titleIndices = result
                .Where(pair => pair.Value.Contains(titleTarget))
                .Select(pair => pair.Key)
                .Where(index => index >= 0 && index < page.Words.Count)
                .Distinct()
                .ToList();

            if (titleIndices.Count == 0)
                continue;

            foreach (var occurrence in Occurrences(titleIndices))
            {
                var titleWords = occurrence.Select(index => page.Words[index]).ToList();
                var right = titleWords.Max(word => word.Right);
                var lineAnchor = titleWords.OrderByDescending(word => word.Right).First();
                var pageWord = page.Words
                    .Select((word, index) => (word, index))
                    .Where(item => !occurrence.Contains(item.index)
                        && item.word.Left > right
                        && IsPageNumber(item.word.Text)
                        && IsSameLine(lineAnchor, item.word))
                    .OrderByDescending(item => item.word.Right)
                    .FirstOrDefault();

                if (pageWord.word is null)
                    continue;

                if (!result.TryGetValue(pageWord.index, out var targets))
                    result[pageWord.index] = targets = [];

                var pageTarget = PageTarget(chapterTitles[rowIndex]);
                if (!targets.Contains(pageTarget))
                    targets.Add(pageTarget);
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<DossierPreviewTarget>)pair.Value);
    }

    private static bool IsSameLine(
        DossierOutputPreviewWord left,
        DossierOutputPreviewWord right)
    {
        var leftCenter = (left.Bottom + left.Top) / 2;
        var rightCenter = (right.Bottom + right.Top) / 2;
        var tolerance = Math.Max(
            Math.Max(1, left.Top - left.Bottom),
            Math.Max(1, right.Top - right.Bottom));

        return Math.Abs(leftCenter - rightCenter) <= tolerance;
    }

    private static bool IsPageNumber(string? text)
    {
        var value = (text ?? string.Empty).Trim().Trim('.', ',', ':');
        return value.Length > 0
            && value.Any(char.IsDigit)
            && value.All(character => char.IsLetterOrDigit(character)
                || character is '-' or '–' or '/');
    }

    private static IEnumerable<IReadOnlyList<int>> Occurrences(
        IReadOnlyList<int> indices)
    {
        if (indices.Count == 0)
            yield break;

        var sorted = indices.OrderBy(index => index).ToList();
        var current = new List<int> { sorted[0] };
        for (var index = 1; index < sorted.Count; index++)
        {
            if (sorted[index] <= sorted[index - 1] + 2)
            {
                current.Add(sorted[index]);
                continue;
            }

            yield return current;
            current = [sorted[index]];
        }

        yield return current;
    }
}
