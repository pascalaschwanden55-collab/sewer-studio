using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Eine echte PDF-Seite mit den Vorlagenkapiteln und den bereits sicher
/// erkannten Wortzielen. Der Mapper bleibt damit frei von WPF und Dateizugriff.
/// </summary>
public sealed record DossierOutputPreviewTablePageInput(
    DossierOutputPreviewPage Page,
    IReadOnlyList<DossierPreviewPage> EditorPages,
    IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> Hits);

/// <summary>Die sicheren Tabellenflaechen eines einzelnen PDF-Blatts.</summary>
public sealed record DossierOutputPreviewTablePageMapping(
    IReadOnlySet<DossierPreviewTarget> ReplacedPhysicalTargets,
    IReadOnlyList<DossierOutputPreviewHitArea> Areas);

/// <summary>
/// Ordnet Wiederholtabellen ueber alle PDF-Seiten hinweg zu. Die physische
/// Tabelle liefert ganze Zellflaechen; der fortlaufende Zeilenzeiger verhindert,
/// dass eine Folgeseite wieder bei Zeile null beginnt.
/// </summary>
public static class DossierOutputPreviewTableCellMapper
{
    private const double PixelsToPoints = 72d / 96d;
    private const double GeometryTolerancePoints = 2;
    private const double NestedPaddingPoints = 2;

    private static readonly HashSet<string> SupportedRepeatKeys = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Aenderungen",
        "Eigentuemer",
        "Themen"
    };

    /// <summary>
    /// Baut die Seiteneingaben aus derselben sichtbaren Zielmenge wie die
    /// normale Klickzuordnung. Die UI muss dadurch weder Zeilenoffsets noch
    /// Tabellengeometrie verwalten.
    /// </summary>
    public static IReadOnlyDictionary<int, DossierOutputPreviewTablePageMapping> Build(
        IReadOnlyList<DossierOutputPreviewNavigationItem> pages,
        IEnumerable<DossierPreviewTarget> targets,
        IReadOnlyList<DossierPreviewField> fields,
        IReadOnlyDictionary<string, string> values,
        DossierDefinition dossier,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(rowsFor);

        var allTargets = targets.Distinct().ToList();
        var inputs = pages.Select(item =>
        {
            var visible = DossierOutputPreviewInteractionMapper.TargetsForPages(
                allTargets,
                item.EditorPages);
            var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
                visible,
                fields,
                values,
                dossier,
                rowsFor);
            var hits = DossierOutputPreviewHitMatcher.Match(
                item.OutputPage.Words,
                candidates);
            return new DossierOutputPreviewTablePageInput(
                item.OutputPage,
                item.EditorPages,
                hits);
        }).ToList();

        return Build(inputs, allTargets, rowsFor);
    }

    public static IReadOnlyDictionary<int, DossierOutputPreviewTablePageMapping> Build(
        IReadOnlyList<DossierOutputPreviewTablePageInput> pages,
        IEnumerable<DossierPreviewTarget> visibleTargets,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(visibleTargets);
        ArgumentNullException.ThrowIfNull(rowsFor);

        var available = visibleTargets.ToHashSet();
        var states = new Dictionary<string, TableState>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<int, DossierOutputPreviewTablePageMapping>();

        foreach (var input in pages)
        {
            ArgumentNullException.ThrowIfNull(input);
            var areas = new List<DossierOutputPreviewHitArea>();
            var replacedTargets = new HashSet<DossierPreviewTarget>();

            foreach (var group in Tables(input.EditorPages)
                         .GroupBy(item => item.Table.RepeatKey!, StringComparer.OrdinalIgnoreCase))
            {
                // Zwei gleichnamige Tabellen auf demselben Blatt sind ohne
                // weitere Vorlagenmarke nicht sicher auseinanderzuhalten.
                if (group.Count() != 1)
                    continue;

                var item = group.Single();
                var key = item.Table.RepeatKey!;
                var rows = rowsFor(key);
                if (rows.Count == 0)
                    continue;

                if (!states.TryGetValue(key, out var state))
                {
                    if (!TryCreateState(input, item.Table, out state, out var firstRowTop))
                        continue;

                    states[key] = state;
                    var firstAreas = BuildPageRows(
                        input.Page,
                        item.EditorPage,
                        state,
                        firstRowTop,
                        rows,
                        available);
                    if (firstAreas.Count > 0)
                    {
                        areas.AddRange(firstAreas);
                        RememberPhysicalTargets(replacedTargets, firstAreas, state);
                    }
                    continue;
                }

                if (state.IsBlocked || !state.Matches(item.Table))
                    continue;

                if (state.HasUncertainPageBoundary)
                {
                    if (NextRowsHaveSamePhysicalValues(state, rows))
                    {
                        state.IsBlocked = true;
                        continue;
                    }

                    state.HasUncertainPageBoundary = false;
                }

                var rowTop = TryHeaderGeometry(
                    input.Page,
                    input.Hits,
                    item.Table,
                    out var repeatedGeometry)
                        ? repeatedGeometry.HeaderBottom
                        : ContinuationTop(input.Page, item.EditorPage);

                if (rowTop <= 0 || rowTop > input.Page.Height)
                    continue;

                var pageAreas = BuildPageRows(
                    input.Page,
                    item.EditorPage,
                    state,
                    rowTop,
                    rows,
                    available);
                if (pageAreas.Count > 0)
                {
                    areas.AddRange(pageAreas);
                    RememberPhysicalTargets(replacedTargets, pageAreas, state);
                }
            }

            result[input.Page.Number] = new DossierOutputPreviewTablePageMapping(
                replacedTargets,
                areas);
        }

        return result;
    }

    /// <summary>
    /// Sobald eine physische Tabelle sicher gemappt ist, duerfen die alten
    /// reinen Worttreffer dieser Tabelle nicht darueberliegen. Sie koennen auf
    /// Folgeseiten noch die falsche Zeilennummer tragen. Andere Feld- und
    /// Literalziele am selben Wort bleiben erhalten.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>>
        RemoveMappedTableTargets(
            IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
            IReadOnlySet<DossierPreviewTarget> replacedPhysicalTargets)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(replacedPhysicalTargets);

        if (replacedPhysicalTargets.Count == 0)
            return hits;

        var result = new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>();
        foreach (var (wordIndex, targets) in hits)
        {
            var remaining = targets
                .Where(target => !IsReplaced(target, replacedPhysicalTargets))
                .Distinct()
                .ToList();
            if (remaining.Count > 0)
                result[wordIndex] = remaining;
        }

        return result;
    }

    private static bool IsReplaced(
        DossierPreviewTarget target,
        IReadOnlySet<DossierPreviewTarget> replacedPhysicalTargets)
    {
        if (target.Kind == DossierPreviewTargetKind.RowCell)
            return replacedPhysicalTargets.Contains(target);

        return target.Kind == DossierPreviewTargetKind.Row
            && replacedPhysicalTargets.Any(replaced =>
                string.Equals(replaced.Key, target.Key, StringComparison.OrdinalIgnoreCase)
                && replaced.RowIndex == target.RowIndex);
    }

    private static void RememberPhysicalTargets(
        ISet<DossierPreviewTarget> destination,
        IEnumerable<DossierOutputPreviewHitArea> areas,
        TableState state)
    {
        var physicalCellKeys = state.CellKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var target in areas
                     .Select(area => area.Target)
                     .Where(target => target.Kind == DossierPreviewTargetKind.RowCell
                         && physicalCellKeys.Contains(target.CellKey)))
        {
            destination.Add(target);
        }
    }

    private static IEnumerable<TableOnPage> Tables(
        IReadOnlyList<DossierPreviewPage> editorPages)
        => editorPages
            .SelectMany(editorPage => editorPage.Blocks
                .OfType<DossierPreviewTable>()
                .Where(table => table.RepeatKey is not null
                    && SupportedRepeatKeys.Contains(table.RepeatKey))
                .Select(table => new TableOnPage(editorPage, table)));

    private static bool TryCreateState(
        DossierOutputPreviewTablePageInput input,
        DossierPreviewTable table,
        out TableState state,
        out double rowTop)
    {
        state = null!;
        rowTop = 0;

        if (!IsSupportedShape(table)
            || !TryHeaderGeometry(input.Page, input.Hits, table, out var geometry))
        {
            return false;
        }

        var defaultHeight = RepeatRowHeightPoints(table.RepeatTemplate!);
        if (defaultHeight <= 0)
            return false;

        state = new TableState(
            table.RepeatKey!,
            table.RepeatCellKeys.ToList(),
            geometry.ColumnLefts,
            table.ColumnWidthsPx
                .Take(table.RepeatCellKeys.Count)
                .Select(width => width * PixelsToPoints)
                .ToList(),
            table.RepeatTemplate!.Cells
                .Select(cell => cell.Padding)
                .ToList(),
            defaultHeight);
        rowTop = geometry.HeaderBottom;
        return true;
    }

    private static bool IsSupportedShape(DossierPreviewTable table)
        => table.RepeatTemplate is not null
            && table.RepeatIndex > 0
            && table.RepeatIndex <= table.Rows.Count
            && table.RepeatCellKeys.Count > 0
            && table.RepeatTemplate.Cells.Count == table.RepeatCellKeys.Count
            && table.ColumnWidthsPx.Count >= table.RepeatCellKeys.Count
            && table.RepeatTemplate.Cells.All(cell => cell.GridSpan == 1)
            && table.Rows[table.RepeatIndex - 1].Cells.Count
                == table.RepeatCellKeys.Count;

    private static IReadOnlyList<DossierOutputPreviewHitArea> BuildPageRows(
        DossierOutputPreviewPage page,
        DossierPreviewPage editorPage,
        TableState state,
        double firstRowTop,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlySet<DossierPreviewTarget> available)
    {
        var result = new List<DossierOutputPreviewHitArea>();
        var rowTop = firstRowTop;
        var bottomLimit = Math.Clamp(
            editorPage.Geometry.Margin.Bottom * PixelsToPoints,
            0,
            page.Height);
        var consumedWords = new HashSet<int>();

        while (state.NextRowIndex < rows.Count)
        {
            // Die Vorlagenzeile ist die sichere Mindesthoehe. Passt sie nicht
            // mehr auf das Blatt, ist dies ein normaler Seitenumbruch und
            // keine inhaltliche Abweichung.
            if (rowTop - state.DefaultHeight <= bottomLimit)
                break;

            var rowIndex = state.NextRowIndex;
            var row = rows[rowIndex];
            var anchor = FindRowAnchor(
                page,
                state,
                row,
                rowTop,
                bottomLimit,
                consumedWords);
            if (anchor.Kind == RowAnchorKind.Uncertain)
            {
                state.HasUncertainPageBoundary = true;
                break;
            }

            var rowBottom = RowBottom(
                rowTop,
                anchor.Bounds,
                state.MaximumPadding,
                state.DefaultHeight);
            if (rowBottom is not { } safeBottom
                || safeBottom <= bottomLimit
                || safeBottom >= rowTop)
            {
                break;
            }

            for (var columnIndex = 0;
                 columnIndex < state.CellKeys.Count;
                 columnIndex++)
            {
                var target = DossierPreviewTarget.RowCell(
                    state.Key,
                    rowIndex,
                    state.CellKeys[columnIndex]);
                if (!available.Contains(target))
                    continue;

                result.Add(new DossierOutputPreviewHitArea(
                    target,
                    Math.Clamp(state.ColumnLefts[columnIndex], 0, page.Width),
                    Math.Clamp(safeBottom, 0, page.Height),
                    Math.Clamp(
                        state.ColumnLefts[columnIndex] + state.ColumnWidths[columnIndex],
                        0,
                        page.Width),
                    Math.Clamp(rowTop, 0, page.Height)));
            }

            result.AddRange(BuildNestedAreas(
                page,
                state,
                row,
                rowIndex,
                safeBottom,
                rowTop,
                available));

            foreach (var wordIndex in anchor.WordIndices)
                consumedWords.Add(wordIndex);

            state.NextRowIndex++;
            rowTop = safeBottom;
        }

        return result;
    }

    private static bool NextRowsHaveSamePhysicalValues(
        TableState state,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var current = state.NextRowIndex;
        if (current < 0 || current + 1 >= rows.Count)
            return false;

        return state.CellKeys.All(key => string.Equals(
            Normalize(rows[current].TryGetValue(key, out var first) ? first : string.Empty),
            Normalize(rows[current + 1].TryGetValue(key, out var second) ? second : string.Empty),
            StringComparison.Ordinal));
    }

    private static RowAnchor FindRowAnchor(
        DossierOutputPreviewPage page,
        TableState state,
        IReadOnlyDictionary<string, string> row,
        double rowTop,
        double bottomLimit,
        IReadOnlySet<int> consumedWords)
    {
        var selected = new List<int>();
        var hasValue = false;

        for (var columnIndex = 0;
             columnIndex < state.CellKeys.Count;
             columnIndex++)
        {
            var cellKey = state.CellKeys[columnIndex];
            if (!row.TryGetValue(cellKey, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            hasValue = true;
            var occurrences = FindOccurrencesInBounds(
                page,
                value,
                state.ColumnLefts[columnIndex],
                state.ColumnLefts[columnIndex] + state.ColumnWidths[columnIndex],
                bottomLimit,
                rowTop,
                consumedWords);
            var occurrence = occurrences.FirstOrDefault();
            if (occurrence is null)
                return new RowAnchor(RowAnchorKind.Uncertain, null, []);

            var occurrenceBounds = BoundsOf(page.Words, occurrence);
            var maximumSameRowTopGap = Math.Max(24, state.DefaultHeight * 0.75);
            if (occurrenceBounds.Top >= rowTop + GeometryTolerancePoints
                || rowTop - occurrenceBounds.Top > maximumSameRowTopGap)
            {
                // Die Fundstelle beginnt erst in einer anderen physischen
                // Tabellenzeile. Werte verschiedener Zeilen duerfen nie zu
                // einer scheinbar vollstaendigen Zeile gemischt werden.
                return new RowAnchor(RowAnchorKind.Uncertain, null, []);
            }

            selected.AddRange(occurrence);
        }

        if (!hasValue)
            return new RowAnchor(RowAnchorKind.Empty, null, []);

        var indices = selected.Distinct().ToList();
        var bounds = BoundsOf(page.Words, indices);
        var maximumTopGap = Math.Max(24, state.DefaultHeight * 1.5);
        if (bounds.Top >= rowTop + GeometryTolerancePoints
            || rowTop - bounds.Top > maximumTopGap)
        {
            return new RowAnchor(RowAnchorKind.Uncertain, null, []);
        }

        return new RowAnchor(RowAnchorKind.Exact, bounds, indices);
    }

    /// <summary>
    /// Telefon, Mail und Objektbewohner liegen als getrennte Editorziele in
    /// derselben physischen Eigentümerzelle. Sie bekommen nur ihre sichere
    /// Textflaeche, niemals eine erfundene zusaetzliche Tabellenspalte.
    /// </summary>
    private static IReadOnlyList<DossierOutputPreviewHitArea> BuildNestedAreas(
        DossierOutputPreviewPage page,
        TableState state,
        IReadOnlyDictionary<string, string> row,
        int rowIndex,
        double rowBottom,
        double rowTop,
        IReadOnlySet<DossierPreviewTarget> available)
    {
        var result = new List<DossierOutputPreviewHitArea>();
        var physicalKeys = state.CellKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tableLeft = state.ColumnLefts[0];
        var tableRight = state.ColumnLefts[^1] + state.ColumnWidths[^1];

        foreach (var target in available.Where(target =>
                     target.Kind == DossierPreviewTargetKind.RowCell
                     && string.Equals(target.Key, state.Key, StringComparison.OrdinalIgnoreCase)
                     && target.RowIndex == rowIndex
                     && !physicalKeys.Contains(target.CellKey)))
        {
            if (!row.TryGetValue(target.CellKey, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var occurrences = FindOccurrencesInBounds(
                page,
                value,
                tableLeft,
                tableRight,
                rowBottom,
                rowTop,
                new HashSet<int>());
            if (occurrences.Count != 1)
                continue;

            var bounds = BoundsOf(page.Words, occurrences[0]);
            result.Add(new DossierOutputPreviewHitArea(
                target,
                Math.Clamp(bounds.Left - NestedPaddingPoints, 0, page.Width),
                Math.Clamp(bounds.Bottom - NestedPaddingPoints, 0, page.Height),
                Math.Clamp(bounds.Right + NestedPaddingPoints, 0, page.Width),
                Math.Clamp(bounds.Top + NestedPaddingPoints, 0, page.Height)));
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyList<int>> FindOccurrencesInBounds(
        DossierOutputPreviewPage page,
        string text,
        double left,
        double right,
        double bottom,
        double top,
        IReadOnlySet<int> excluded)
    {
        var sought = Normalize(text);
        if (sought.Length == 0)
            return [];

        var words = ReadingOrder(page.Words
            .Select((word, index) => new IndexedWord(index, word, Normalize(word.Text)))
            .Where(item => item.Token.Length > 0
                && !excluded.Contains(item.Index)
                && HorizontalCenter(item.Word) >= left - GeometryTolerancePoints
                && HorizontalCenter(item.Word) <= right + GeometryTolerancePoints
                && item.Word.Bottom >= bottom - GeometryTolerancePoints
                && item.Word.Top <= top + GeometryTolerancePoints));
        var result = new List<IReadOnlyList<int>>();

        for (var start = 0; start < words.Count; start++)
        {
            var combined = string.Empty;
            var indices = new List<int>();
            for (var end = start; end < words.Count; end++)
            {
                combined += words[end].Token;
                indices.Add(words[end].Index);
                if (combined.Length > sought.Length)
                    break;

                if (string.Equals(combined, sought, StringComparison.Ordinal))
                {
                    result.Add(indices);
                    break;
                }
            }
        }

        return result
            .OrderByDescending(indices => BoundsOf(page.Words, indices).Top)
            .ThenBy(indices => BoundsOf(page.Words, indices).Left)
            .ToList();
    }

    private static IReadOnlyList<IndexedWord> ReadingOrder(
        IEnumerable<IndexedWord> source)
    {
        var lines = new List<List<IndexedWord>>();
        foreach (var item in source
                     .OrderByDescending(value => VerticalCenter(value.Word))
                     .ThenBy(value => value.Word.Left))
        {
            var line = lines.FirstOrDefault(existing => IsSameLine(
                existing[0].Word,
                item.Word));
            if (line is null)
            {
                line = [];
                lines.Add(line);
            }

            line.Add(item);
        }

        return lines
            .OrderByDescending(line => line.Average(item => VerticalCenter(item.Word)))
            .SelectMany(line => line.OrderBy(item => item.Word.Left))
            .ToList();
    }

    private static bool TryHeaderGeometry(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        DossierPreviewTable table,
        out HeaderGeometry geometry)
    {
        geometry = default;
        if (!IsSupportedShape(table))
            return false;

        var header = table.Rows[table.RepeatIndex - 1];
        var bounds = new List<Bounds>(header.Cells.Count);
        for (var index = 0; index < header.Cells.Count; index++)
        {
            var occurrences = HeaderOccurrences(page, hits, CellText(header.Cells[index]));
            if (occurrences.Count != 1)
                return false;

            bounds.Add(BoundsOf(page.Words, occurrences[0]));
        }

        if (!AreOnSameLine(bounds)
            || !bounds.Zip(bounds.Skip(1), (first, second) => first.Left < second.Left)
                .All(value => value))
        {
            return false;
        }

        var left = bounds[0].Left - header.Cells[0].Padding.Left * PixelsToPoints;
        var headerBottom = bounds
            .Select((cellBounds, index) => cellBounds.Bottom
                - (header.Cells[index].Padding.Bottom
                   + ParagraphSpaceAfter(header.Cells[index])) * PixelsToPoints)
            .Min();
        var columnLefts = new List<double>(table.RepeatCellKeys.Count);
        var currentLeft = left;
        for (var index = 0; index < table.RepeatCellKeys.Count; index++)
        {
            columnLefts.Add(currentLeft);
            currentLeft += table.ColumnWidthsPx[index] * PixelsToPoints;
        }

        if (left < 0
            || headerBottom <= 0
            || headerBottom > page.Height
            || currentLeft > page.Width + GeometryTolerancePoints)
        {
            return false;
        }

        geometry = new HeaderGeometry(columnLefts, headerBottom);
        return true;
    }

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
                     .OrderByDescending(index => VerticalCenter(page.Words[index]))
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
            if (tokens.Select((token, offset) => string.Equals(
                    normalized[start + offset],
                    token,
                    StringComparison.Ordinal)).All(value => value))
            {
                result.Add(Enumerable.Range(start, tokens.Count).ToList());
            }
        }

        return result;
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
        if (actual.Value.Top >= rowTop + GeometryTolerancePoints
            || actual.Value.Bottom <= rowTop - maximumHeight)
        {
            return null;
        }

        var result = Math.Min(
            actual.Value.Bottom - padding.Bottom * PixelsToPoints,
            rowTop - Math.Max(defaultHeight, measuredHeight));
        return result >= rowTop - maximumHeight ? result : null;
    }

    private static double ContinuationTop(
        DossierOutputPreviewPage page,
        DossierPreviewPage editorPage)
        => page.Height - editorPage.Geometry.Margin.Top * PixelsToPoints;

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
        DossierOutputPreviewWord first,
        DossierOutputPreviewWord second)
    {
        var tolerance = Math.Max(
            Math.Max(1, first.Top - first.Bottom),
            Math.Max(1, second.Top - second.Bottom));
        return Math.Abs(VerticalCenter(first) - VerticalCenter(second)) <= tolerance;
    }

    private static double HorizontalCenter(DossierOutputPreviewWord word)
        => (word.Left + word.Right) / 2;

    private static double VerticalCenter(DossierOutputPreviewWord word)
        => (word.Bottom + word.Top) / 2;

    private sealed record TableOnPage(
        DossierPreviewPage EditorPage,
        DossierPreviewTable Table);

    private sealed class TableState
    {
        public TableState(
            string key,
            IReadOnlyList<string> cellKeys,
            IReadOnlyList<double> columnLefts,
            IReadOnlyList<double> columnWidths,
            IReadOnlyList<DossierPreviewEdges> paddings,
            double defaultHeight)
        {
            Key = key;
            CellKeys = cellKeys;
            ColumnLefts = columnLefts;
            ColumnWidths = columnWidths;
            DefaultHeight = defaultHeight;
            MaximumPadding = new DossierPreviewEdges(
                paddings.Max(value => value.Left),
                paddings.Max(value => value.Top),
                paddings.Max(value => value.Right),
                paddings.Max(value => value.Bottom));
        }

        public string Key { get; }
        public IReadOnlyList<string> CellKeys { get; }
        public IReadOnlyList<double> ColumnLefts { get; }
        public IReadOnlyList<double> ColumnWidths { get; }
        public DossierPreviewEdges MaximumPadding { get; }
        public double DefaultHeight { get; }
        public int NextRowIndex { get; set; }
        public bool HasUncertainPageBoundary { get; set; }
        public bool IsBlocked { get; set; }

        public bool Matches(DossierPreviewTable table)
            => string.Equals(table.RepeatKey, Key, StringComparison.OrdinalIgnoreCase)
                && table.RepeatCellKeys.SequenceEqual(
                    CellKeys,
                    StringComparer.OrdinalIgnoreCase)
                && table.ColumnWidthsPx
                    .Take(CellKeys.Count)
                    .Select(width => width * PixelsToPoints)
                    .SequenceEqual(ColumnWidths);
    }

    private sealed record IndexedWord(
        int Index,
        DossierOutputPreviewWord Word,
        string Token);

    private readonly record struct HeaderGeometry(
        IReadOnlyList<double> ColumnLefts,
        double HeaderBottom);

    private readonly record struct Bounds(
        double Left,
        double Bottom,
        double Right,
        double Top);

    private sealed record RowAnchor(
        RowAnchorKind Kind,
        Bounds? Bounds,
        IReadOnlyList<int> WordIndices);

    private enum RowAnchorKind
    {
        Empty,
        Exact,
        Uncertain
    }
}
