using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Ergaenzt Klickflaechen fuer leere Zellen einer Wiederholzeile. PdfPig kann
/// dort kein Wort liefern; Lage und Breite werden deshalb nur dann aus der
/// eindeutig erkannten Tabellenkopfzeile und dem Vorlagenbauplan abgeleitet.
/// </summary>
public static class DossierOutputPreviewEmptyRowCellMapper
{
    private const double PixelsToPoints = 72d / 96d;

    public static IReadOnlyList<DossierOutputPreviewHitArea> Build(
        DossierOutputPreviewPage page,
        IReadOnlyList<DossierPreviewPage> editorPages,
        IEnumerable<DossierPreviewTarget> visibleTargets,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(editorPages);
        ArgumentNullException.ThrowIfNull(visibleTargets);
        ArgumentNullException.ThrowIfNull(rowsFor);
        ArgumentNullException.ThrowIfNull(hits);

        var tables = editorPages
            .SelectMany(editorPage => editorPage.Blocks)
            .OfType<DossierPreviewTable>()
            .Where(table => string.Equals(
                table.RepeatKey,
                "Aenderungen",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Mehrere moegliche Tabellen oder ein unvollstaendiger Bauplan sind
        // nicht sicher zuordenbar. Dann bleibt die Vorschau unveraendert.
        if (tables.Count != 1)
            return [];

        var table = tables[0];
        if (table.RepeatTemplate is null
            || table.RepeatIndex <= 0
            || table.RepeatIndex > table.Rows.Count
            || table.RepeatCellKeys.Count == 0
            || table.RepeatTemplate.Cells.Count != table.RepeatCellKeys.Count
            || table.ColumnWidthsPx.Count < table.RepeatCellKeys.Count
            || table.RepeatTemplate.Cells.Any(cell => cell.GridSpan != 1))
        {
            return [];
        }

        var header = table.Rows[table.RepeatIndex - 1];
        if (header.Cells.Count != table.RepeatCellKeys.Count)
            return [];

        var headerBounds = new List<Bounds>(header.Cells.Count);
        for (var index = 0; index < header.Cells.Count; index++)
        {
            var text = CellText(header.Cells[index]);
            var occurrences = HeaderOccurrences(page, hits, text);
            if (occurrences.Count != 1)
                return [];

            headerBounds.Add(BoundsOf(page.Words, occurrences[0]));
        }

        if (!AreOnSameLine(headerBounds)
            || !headerBounds.Zip(headerBounds.Skip(1), (left, right) => left.Left < right.Left)
                .All(value => value))
        {
            return [];
        }

        var rows = rowsFor(table.RepeatKey!);
        if (rows.Count == 0)
            return [];

        var available = visibleTargets.ToHashSet();
        var alreadyMatched = hits.Values.SelectMany(targets => targets).ToHashSet();
        var repeatPadding = table.RepeatTemplate.Cells[0].Padding;
        var defaultHeight = RepeatRowHeightPoints(table.RepeatTemplate);
        if (defaultHeight <= 0)
            return [];

        var left = headerBounds[0].Left
            - header.Cells[0].Padding.Left * PixelsToPoints;
        var headerBottom = headerBounds
            .Select((bounds, index) => bounds.Bottom
                - (header.Cells[index].Padding.Bottom
                   + ParagraphSpaceAfter(header.Cells[index])) * PixelsToPoints)
            .Min();

        if (left < 0 || headerBottom <= 0 || headerBottom > page.Height)
            return [];

        var columnLefts = new double[table.RepeatCellKeys.Count];
        var currentLeft = left;
        for (var index = 0; index < columnLefts.Length; index++)
        {
            columnLefts[index] = currentLeft;
            currentLeft += table.ColumnWidthsPx[index] * PixelsToPoints;
        }

        if (currentLeft > page.Width + 2)
            return [];

        var result = new List<DossierOutputPreviewHitArea>();
        var previousBottom = headerBottom;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = RowBounds(page, hits, table.RepeatKey!, rowIndex);
            var rowTop = actual is null
                ? previousBottom
                : Math.Min(previousBottom, actual.Value.Top
                    + repeatPadding.Top * PixelsToPoints);
            var rowBottom = actual is null
                ? rowTop - defaultHeight
                : actual.Value.Bottom - repeatPadding.Bottom * PixelsToPoints;

            if (rowBottom < 0 || rowBottom >= rowTop)
                return [];

            for (var columnIndex = 0;
                 columnIndex < table.RepeatCellKeys.Count;
                 columnIndex++)
            {
                var cellKey = table.RepeatCellKeys[columnIndex];
                var target = DossierPreviewTarget.RowCell(
                    table.RepeatKey!, rowIndex, cellKey);

                if (!available.Contains(target)
                    || alreadyMatched.Contains(target)
                    || rows[rowIndex].TryGetValue(cellKey, out var value)
                        && !string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                result.Add(new DossierOutputPreviewHitArea(
                    target,
                    Math.Clamp(columnLefts[columnIndex], 0, page.Width),
                    Math.Clamp(rowBottom, 0, page.Height),
                    Math.Clamp(
                        columnLefts[columnIndex]
                        + table.ColumnWidthsPx[columnIndex] * PixelsToPoints,
                        0,
                        page.Width),
                    Math.Clamp(rowTop, 0, page.Height)));
            }

            previousBottom = rowBottom;
        }

        return result;
    }

    /// <summary>
    /// Ein Tabellenkopf ist selbst bearbeitbar. Der normale Treffermatcher
    /// kennt auch nach einer Umbenennung weiterhin dessen urspruengliche
    /// Zieladresse; diese Treffer sind deshalb die verlaesslichste Lagequelle.
    /// Nur fuer alte Aufrufer ohne Literal-Treffer bleibt die exakte Textsuche
    /// als Rueckfall erhalten.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<int>> HeaderOccurrences(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        string originalText)
    {
        var target = DossierPreviewTarget.Literal(originalText);
        var matched = hits
            .Where(pair => pair.Key >= 0
                && pair.Key < page.Words.Count
                && pair.Value.Contains(target))
            .Select(pair => pair.Key)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        if (matched.Count == 0)
            return FindOccurrences(page.Words, originalText);

        var lines = new List<List<int>>();
        foreach (var wordIndex in matched
                     .OrderByDescending(index => WordCenter(page.Words[index]))
                     .ThenBy(index => page.Words[index].Left))
        {
            var line = lines.FirstOrDefault(existing => IsSameLine(
                page.Words[existing[0]],
                page.Words[wordIndex]));
            if (line is null)
            {
                line = [];
                lines.Add(line);
            }

            line.Add(wordIndex);
        }

        return lines
            .Select(line => (IReadOnlyList<int>)line
                .OrderBy(index => page.Words[index].Left)
                .ToList())
            .ToList();
    }

    private static Bounds? RowBounds(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        string key,
        int rowIndex)
    {
        var indices = hits
            .Where(pair => pair.Value.Any(target =>
                target.RowIndex == rowIndex
                && string.Equals(target.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Key)
            .Where(index => index >= 0 && index < page.Words.Count)
            .Distinct()
            .ToList();

        return indices.Count == 0 ? null : BoundsOf(page.Words, indices);
    }

    private static double RepeatRowHeightPoints(DossierPreviewTableRow row)
    {
        var heightPx = row.Cells.Max(cell =>
            cell.Padding.Top
            + cell.Padding.Bottom
            + cell.Borders.Top
            + cell.Borders.Bottom
            + cell.Paragraphs.Sum(ParagraphHeightPx));

        return heightPx * PixelsToPoints;
    }

    private static double ParagraphHeightPx(DossierPreviewParagraph paragraph)
    {
        var fontSize = paragraph.Runs.Count == 0
            ? DossierPreviewRunFormat.Default.FontSizePx
            : paragraph.Runs.Max(run => run.Format.FontSizePx);
        var lineHeight = paragraph.Format.LineHeightPx ?? fontSize * 1.2;
        return paragraph.Format.SpaceBeforePx + lineHeight + paragraph.Format.SpaceAfterPx;
    }

    private static double ParagraphSpaceAfter(DossierPreviewTableCell cell)
        => cell.Paragraphs.LastOrDefault()?.Format.SpaceAfterPx ?? 0;

    private static string CellText(DossierPreviewTableCell cell)
        => string.Concat(cell.Paragraphs.SelectMany(paragraph => paragraph.Runs)
                .Select(run => run.Text))
            .Trim();

    private static IReadOnlyList<IReadOnlyList<int>> FindOccurrences(
        IReadOnlyList<DossierOutputPreviewWord> words,
        string text)
    {
        var tokens = Tokens(text);
        if (tokens.Count == 0)
            return [];

        var normalized = words.Select(word => Normalize(word.Text)).ToList();
        var result = new List<IReadOnlyList<int>>();
        for (var start = 0; start <= normalized.Count - tokens.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < tokens.Count; offset++)
            {
                if (!string.Equals(
                        normalized[start + offset],
                        tokens[offset],
                        StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                result.Add(Enumerable.Range(start, tokens.Count).ToList());
        }

        return result;
    }

    private static IReadOnlyList<string> Tokens(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(token => token.Length > 0)
            .ToList();

    private static string Normalize(string? text)
        => new((text ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static Bounds BoundsOf(
        IReadOnlyList<DossierOutputPreviewWord> words,
        IReadOnlyList<int> indices)
    {
        var selected = indices.Select(index => words[index]).ToList();
        return new Bounds(
            selected.Min(word => word.Left),
            selected.Min(word => word.Bottom),
            selected.Max(word => word.Right),
            selected.Max(word => word.Top));
    }

    private static bool AreOnSameLine(IReadOnlyList<Bounds> bounds)
    {
        var centers = bounds.Select(value => (value.Bottom + value.Top) / 2).ToList();
        var tolerance = bounds.Max(value => Math.Max(1, value.Top - value.Bottom));
        return centers.Max() - centers.Min() <= tolerance;
    }

    private static bool IsSameLine(
        DossierOutputPreviewWord left,
        DossierOutputPreviewWord right)
    {
        var tolerance = Math.Max(
            Math.Max(1, left.Top - left.Bottom),
            Math.Max(1, right.Top - right.Bottom));
        return Math.Abs(WordCenter(left) - WordCenter(right)) <= tolerance;
    }

    private static double WordCenter(DossierOutputPreviewWord word)
        => (word.Bottom + word.Top) / 2;

    private readonly record struct Bounds(
        double Left,
        double Bottom,
        double Right,
        double Top);
}
