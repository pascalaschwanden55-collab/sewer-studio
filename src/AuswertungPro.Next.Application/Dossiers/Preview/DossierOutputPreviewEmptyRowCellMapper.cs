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
    private static readonly HashSet<string> SupportedRepeatKeys = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Aenderungen",
        "Eigentuemer",
        "Themen"
    };

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

        var tableGroups = editorPages
            .SelectMany(editorPage => editorPage.Blocks)
            .OfType<DossierPreviewTable>()
            .Where(table => table.RepeatKey is not null
                && SupportedRepeatKeys.Contains(table.RepeatKey))
            .GroupBy(table => table.RepeatKey!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var available = visibleTargets.ToHashSet();
        var alreadyMatched = hits.Values.SelectMany(targets => targets).ToHashSet();
        var result = new List<DossierOutputPreviewHitArea>();

        foreach (var group in tableGroups)
        {
            // Eine mehrdeutige Tabelle bleibt unangetastet. Eine andere,
            // eindeutige Tabelle auf demselben PDF-Blatt darf dadurch aber
            // nicht ebenfalls ihre sicheren Klickflaechen verlieren.
            if (group.Count() != 1)
                continue;

            var table = group.Single();
            result.AddRange(BuildTable(
                page,
                table,
                available,
                rowsFor(table.RepeatKey!),
                hits,
                alreadyMatched));
        }

        return result;
    }

    private static IReadOnlyList<DossierOutputPreviewHitArea> BuildTable(
        DossierOutputPreviewPage page,
        DossierPreviewTable table,
        IReadOnlySet<DossierPreviewTarget> available,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        IReadOnlySet<DossierPreviewTarget> alreadyMatched)
    {
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

        if (rows.Count == 0)
            return [];

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

        var rowAnchors = new RowAnchor[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            rowAnchors[rowIndex] = TrustedRowBounds(
                page,
                hits,
                table.RepeatKey!,
                rowIndex,
                table.RepeatCellKeys,
                rows[rowIndex],
                columnLefts,
                table.ColumnWidthsPx,
                headerBottom);
        }

        var result = new List<DossierOutputPreviewHitArea>();
        var previousBottom = headerBottom;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var anchor = rowAnchors[rowIndex];
            if (anchor.Kind == RowAnchorKind.Uncertain)
                break;

            var actual = anchor.Bounds;
            var rowTop = previousBottom;
            var rowBottom = RowBottom(
                rowTop,
                actual,
                repeatPadding,
                defaultHeight);

            if (rowBottom is not { } safeBottom)
                break;

            if (safeBottom < 0 || safeBottom >= rowTop)
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
                    Math.Clamp(safeBottom, 0, page.Height),
                    Math.Clamp(
                        columnLefts[columnIndex]
                        + table.ColumnWidthsPx[columnIndex] * PixelsToPoints,
                        0,
                        page.Width),
                    Math.Clamp(rowTop, 0, page.Height)));
            }

            previousBottom = safeBottom;
        }

        return result;
    }

    /// <summary>
    /// Nutzt nur vollstaendig erkannte Zellentexte als Hoehenanker. Der normale
    /// Wortmatcher darf bei langen Texten bewusst nur einen eindeutigen
    /// Ausschnitt liefern; dieser Ausschnitt waere fuer die Zeilenhoehe zu
    /// ungenau. Bei einer leeren Bemerkung reicht dagegen der vollstaendig
    /// erkannte Thementitel, um sie sicher derselben Zeile zuzuordnen.
    /// </summary>
    private static RowAnchor TrustedRowBounds(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        string key,
        int rowIndex,
        IReadOnlyList<string> cellKeys,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyList<double> columnLefts,
        IReadOnlyList<double> columnWidthsPx,
        double headerBottom)
    {
        var indices = new List<int>();
        var hasValue = false;
        var incomplete = false;

        for (var columnIndex = 0; columnIndex < cellKeys.Count; columnIndex++)
        {
            var cellKey = cellKeys[columnIndex];
            if (!row.TryGetValue(cellKey, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            hasValue = true;
            var target = DossierPreviewTarget.RowCell(key, rowIndex, cellKey);
            var cellIndices = hits
                .Where(pair => pair.Key >= 0
                    && pair.Key < page.Words.Count
                    && pair.Value.Contains(target))
                .Select(pair => pair.Key)
                .Distinct()
                .OrderBy(index => index)
                .ToList();

            if (cellIndices.Count == 0
                || !string.Equals(
                    Normalize(string.Concat(cellIndices.Select(
                        index => page.Words[index].Text))),
                    Normalize(value),
                    StringComparison.Ordinal))
            {
                incomplete = true;
                continue;
            }

            var bounds = BoundsOf(page.Words, cellIndices);
            var expectedLeft = columnLefts[columnIndex] - 2;
            var expectedRight = columnLefts[columnIndex]
                + columnWidthsPx[columnIndex] * PixelsToPoints
                + 2;
            if (bounds.Left < expectedLeft
                || bounds.Right > expectedRight
                || bounds.Top >= headerBottom
                || bounds.Bottom <= 0)
            {
                incomplete = true;
                continue;
            }

            indices.AddRange(cellIndices);
        }

        var distinct = indices.Distinct().ToList();
        if (!hasValue)
            return new RowAnchor(RowAnchorKind.Empty, null);

        if (incomplete || distinct.Count == 0)
            return new RowAnchor(RowAnchorKind.Uncertain, null);

        return new RowAnchor(RowAnchorKind.Exact, BoundsOf(page.Words, distinct));
    }

    private static double? RowBottom(
        double rowTop,
        Bounds? actual,
        DossierPreviewEdges padding,
        double defaultHeight)
    {
        if (actual is null)
            return rowTop - defaultHeight;

        var contentHeight = actual.Value.Top - actual.Value.Bottom;
        var measuredHeight = contentHeight
            + (padding.Top + padding.Bottom) * PixelsToPoints;
        var maximumHeight = Math.Max(defaultHeight, measuredHeight + defaultHeight);

        // Der Treffer muss in DER Zeile liegen, deren Oberkante aus Kopf und
        // allen vorherigen Zeilen bereits feststeht. Ein gleichlautender Text
        // irgendwo weiter unten wird nicht passend gekuerzt, sondern verworfen.
        if (actual.Value.Top >= rowTop
            || actual.Value.Bottom <= rowTop - maximumHeight)
        {
            return null;
        }

        var result = Math.Min(
            actual.Value.Bottom - padding.Bottom * PixelsToPoints,
            rowTop - Math.Max(defaultHeight, measuredHeight));
        return result >= rowTop - maximumHeight ? result : null;
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

    private static double RepeatRowHeightPoints(DossierPreviewTableRow row)
    {
        var heightPx = row.Cells.Max(cell =>
            cell.Padding.Top
            + cell.Padding.Bottom
            + cell.Borders.Top
            + cell.Borders.Bottom
            + cell.Paragraphs.Sum(ParagraphHeightPx));

        return Math.Max(heightPx, row.MinimumHeightPx ?? 0) * PixelsToPoints;
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

    private readonly record struct RowAnchor(RowAnchorKind Kind, Bounds? Bounds);

    private enum RowAnchorKind
    {
        Empty,
        Exact,
        Uncertain
    }
}
