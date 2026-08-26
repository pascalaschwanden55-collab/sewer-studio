using System;
using System.Collections.Generic;
using System.Globalization;
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

        AddGluedLeaderTitleTargets(page, result, chapterTitles);

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

    /// <summary>
    /// Word und PDFPig liefern die Punktlinie nicht immer als eigenes Wort.
    /// Aus dem letzten Titelwort kann zum Beispiel
    /// <c>Werkleitungen........3</c> werden. Der allgemeine Texttreffer darf
    /// dieses Wort nicht unscharf kuerzen; hier ist die Zuordnung trotzdem
    /// sicher, weil Punktlinie, Seitenzahl, Zeilennummer und ganzer Titel
    /// gemeinsam exakt stimmen muessen.
    /// </summary>
    private static void AddGluedLeaderTitleTargets(
        DossierOutputPreviewPage page,
        IDictionary<int, List<DossierPreviewTarget>> hits,
        IReadOnlyList<string> chapterTitles)
    {
        for (var rowIndex = 0; rowIndex < chapterTitles.Count; rowIndex++)
        {
            var title = chapterTitles[rowIndex];
            var expectedLine = Normalize(
                (rowIndex + 1).ToString(CultureInfo.InvariantCulture) + title);
            var expectedWithoutNumber = Normalize(title);
            var matches = new List<IReadOnlyList<int>>();

            for (var leaderIndex = 0; leaderIndex < page.Words.Count; leaderIndex++)
            {
                if (!TrySplitLeaderAndPage(
                        page.Words[leaderIndex].Text,
                        out var beforeLeader))
                {
                    continue;
                }

                var anchor = page.Words[leaderIndex];
                var line = page.Words
                    .Select((word, index) => (word, index))
                    .Where(item => IsSameLine(anchor, item.word)
                        && item.word.Left <= anchor.Right)
                    .OrderBy(item => item.word.Left)
                    .ThenBy(item => item.index)
                    .ToList();

                var normalizedLine = string.Concat(line.Select(item => Normalize(
                    item.index == leaderIndex ? beforeLeader : item.word.Text)));
                if (!string.Equals(normalizedLine, expectedLine, StringComparison.Ordinal)
                    && !string.Equals(
                        normalizedLine,
                        expectedWithoutNumber,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add(line.Select(item => item.index).ToList());
            }

            // Mehrere gleich passende Zeilen waeren nicht eindeutig. In
            // diesem Fall lieber keinen Klick anbieten als die falsche Zeile
            // zu oeffnen.
            if (matches.Count != 1)
                continue;

            var target = DossierPreviewTarget.Literal(title);
            foreach (var wordIndex in matches[0])
            {
                if (!hits.TryGetValue(wordIndex, out var targets))
                    hits[wordIndex] = targets = [];

                if (!targets.Contains(target))
                    targets.Add(target);
            }
        }
    }

    private static bool TrySplitLeaderAndPage(string? text, out string beforeLeader)
    {
        var value = text ?? string.Empty;
        beforeLeader = string.Empty;

        for (var start = 0; start < value.Length - 1; start++)
        {
            if (value[start] != '.' || value[start + 1] != '.')
                continue;

            var end = start + 2;
            while (end < value.Length && value[end] == '.')
                end++;

            if (!IsPageNumber(value[end..]))
                return false;

            beforeLeader = value[..start];
            return true;
        }

        return false;
    }

    private static string Normalize(string? text)
        => new((text ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

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
