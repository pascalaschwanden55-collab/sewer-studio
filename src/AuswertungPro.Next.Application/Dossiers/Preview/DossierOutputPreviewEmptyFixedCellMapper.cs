using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Ergaenzt Klickflaechen fuer bekannte feste Tabellenfelder, wenn deren Wert
/// leer ist und die PDF deshalb kein Wort zum Treffen liefert. Die Zuordnung
/// bleibt absichtlich auf eindeutig aufgebaute Zellen der Word-Vorlage
/// begrenzt.
/// </summary>
public static class DossierOutputPreviewEmptyFixedCellMapper
{
    private const double PixelsToPoints = 72d / 96d;

    private static readonly Rule[] Rules =
    [
        new(
            "Für die Aktennotiz",
            "Aktennotiz",
            AreaKind.WholeCell),
        // Die Rueckmeldung teilt ihre Zelle mit Ort, Datum, Punktlinien und
        // Unterschrift. Anklickbar ist deshalb nur ihr eigener erster Absatz.
        new(
            "Rückmeldung / Einverständnis Eigentümer",
            "Rueckmeldung",
            AreaKind.FirstParagraph,
            ["Ort/Datum", "Unterschrift(en)"])
    ];

    public static IReadOnlyList<DossierOutputPreviewHitArea> Build(
        DossierOutputPreviewPage page,
        IReadOnlyList<DossierPreviewPage> editorPages,
        IEnumerable<DossierPreviewTarget> visibleTargets,
        IEnumerable<DossierPreviewField> fields,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        Func<string, string>? visibleLiteralFor = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(editorPages);
        ArgumentNullException.ThrowIfNull(visibleTargets);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(hits);

        var available = visibleTargets.ToHashSet();
        var alreadyMatched = hits.Values.SelectMany(targets => targets).ToHashSet();
        var fieldList = fields.ToList();
        var result = new List<DossierOutputPreviewHitArea>();

        foreach (var rule in Rules)
        {
            var target = DossierPreviewTarget.Field(rule.FieldKey);
            if (!available.Contains(target) || alreadyMatched.Contains(target))
                continue;

            var editableFields = fieldList
                .Where(field => field.Write is not null
                    && string.Equals(
                        field.Key,
                        rule.FieldKey,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (editableFields.Count != 1
                || !string.IsNullOrWhiteSpace(editableFields[0].Read()))
            {
                continue;
            }

            var candidates = FindCandidates(editorPages, rule).ToList();
            if (candidates.Count != 1)
                continue;

            var visibleAnchor = visibleLiteralFor?.Invoke(rule.AnchorText)
                ?? rule.AnchorText;
            var anchorIndices = AnchorIndices(
                page,
                hits,
                rule.AnchorText,
                visibleAnchor);
            if (anchorIndices.Count == 0)
                continue;

            var area = CreateArea(page, candidates[0], anchorIndices, target, rule.Kind);
            if (area is not null)
                result.Add(area);
        }

        return result;
    }

    private static IEnumerable<Candidate> FindCandidates(
        IReadOnlyList<DossierPreviewPage> pages,
        Rule rule)
    {
        foreach (var table in pages
                     .SelectMany(page => page.Blocks)
                     .OfType<DossierPreviewTable>())
        {
            if (table.ColumnWidthsPx.Count != 2)
                continue;

            foreach (var row in table.Rows)
            {
                if (row.Cells.Count != 2
                    || row.Cells.Any(cell => cell.GridSpan != 1)
                    || !IsAnchorCell(row.Cells[0], rule.AnchorText)
                    || !IsTargetCell(row.Cells[1], rule))
                {
                    continue;
                }

                yield return new Candidate(table, row, row.Cells[0], row.Cells[1]);
            }
        }
    }

    private static bool IsAnchorCell(DossierPreviewTableCell cell, string expected)
    {
        var runs = cell.Paragraphs.SelectMany(paragraph => paragraph.Runs).ToList();
        return runs.All(run => !run.IsField)
            && string.Equals(
                Normalize(string.Concat(runs.Select(run => run.Text))),
                Normalize(expected),
                StringComparison.Ordinal);
    }

    private static bool IsTargetCell(DossierPreviewTableCell cell, Rule rule)
    {
        if (cell.Paragraphs.Count == 0)
            return false;

        var firstRuns = cell.Paragraphs[0].Runs;
        if (firstRuns.Count(run => run.IsField) != 1
            || firstRuns.Any(run => run.IsField
                && !string.Equals(
                    run.FieldKey,
                    rule.FieldKey,
                    StringComparison.OrdinalIgnoreCase))
            || firstRuns.Any(run => !run.IsField
                && !string.IsNullOrWhiteSpace(run.Text)))
        {
            return false;
        }

        var remainingRuns = cell.Paragraphs
            .Skip(1)
            .SelectMany(paragraph => paragraph.Runs)
            .ToList();
        if (remainingRuns.Any(run => run.IsField))
            return false;

        if (rule.Kind == AreaKind.WholeCell)
            return remainingRuns.All(run => string.IsNullOrWhiteSpace(run.Text));

        var trailingText = Normalize(string.Concat(remainingRuns.Select(run => run.Text)));
        return rule.RequiredTrailingTexts.All(required =>
            trailingText.Contains(Normalize(required), StringComparison.Ordinal));
    }

    private static IReadOnlyList<int> AnchorIndices(
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        string originalAnchor,
        string visibleAnchor)
    {
        if (string.IsNullOrWhiteSpace(visibleAnchor))
            return [];

        var target = DossierPreviewTarget.Literal(originalAnchor);
        var matched = hits
            .Where(pair => pair.Key >= 0
                && pair.Key < page.Words.Count
                && pair.Value.Contains(target))
            .Select(pair => pair.Key)
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        if (matched.Count > 0)
        {
            return string.Equals(
                    Normalize(string.Concat(matched.Select(index => page.Words[index].Text))),
                    Normalize(visibleAnchor),
                    StringComparison.Ordinal)
                ? matched
                : [];
        }

        var occurrences = FindOccurrences(page.Words, visibleAnchor);
        return occurrences.Count == 1 ? occurrences[0] : [];
    }

    private static DossierOutputPreviewHitArea? CreateArea(
        DossierOutputPreviewPage page,
        Candidate candidate,
        IReadOnlyList<int> anchorIndices,
        DossierPreviewTarget target,
        AreaKind kind)
    {
        var anchor = BoundsOf(page.Words, anchorIndices);
        var tableLeft = anchor.Left
            - candidate.AnchorCell.Padding.Left * PixelsToPoints;
        var targetLeft = tableLeft
            + candidate.Table.ColumnWidthsPx[0] * PixelsToPoints;
        var targetRight = targetLeft
            + candidate.Table.ColumnWidthsPx[1] * PixelsToPoints;
        var rowTop = anchor.Top
            + candidate.AnchorCell.Padding.Top * PixelsToPoints;
        var rowHeight = RowHeightPoints(candidate.Row);
        var rowBottom = rowTop - rowHeight;

        if (tableLeft < 0
            || targetLeft <= tableLeft
            || targetRight <= targetLeft
            || targetRight > page.Width + 2
            || rowTop <= 0
            || rowTop > page.Height
            || rowBottom < 0
            || rowBottom >= rowTop
            || anchor.Right > targetLeft + 2
            || anchor.Bottom < rowBottom - 2)
        {
            return null;
        }

        var bottom = kind == AreaKind.WholeCell
            ? rowBottom
            : rowTop
                - candidate.TargetCell.Padding.Top * PixelsToPoints
                - ParagraphHeightPoints(candidate.TargetCell.Paragraphs[0]);

        if (bottom <= rowBottom - 2 || bottom >= rowTop)
            return null;

        return new DossierOutputPreviewHitArea(
            target,
            Math.Clamp(targetLeft, 0, page.Width),
            Math.Clamp(bottom, 0, page.Height),
            Math.Clamp(targetRight, 0, page.Width),
            Math.Clamp(rowTop, 0, page.Height));
    }

    private static double RowHeightPoints(DossierPreviewTableRow row)
    {
        var heightPx = row.Cells.Max(cell =>
            cell.Padding.Top
            + cell.Padding.Bottom
            + cell.Borders.Top
            + cell.Borders.Bottom
            + cell.Paragraphs.Sum(ParagraphHeightPx));

        return Math.Max(heightPx, row.MinimumHeightPx ?? 0) * PixelsToPoints;
    }

    private static double ParagraphHeightPoints(DossierPreviewParagraph paragraph)
        => ParagraphHeightPx(paragraph) * PixelsToPoints;

    private static double ParagraphHeightPx(DossierPreviewParagraph paragraph)
    {
        var fontSize = paragraph.Runs.Count == 0
            ? DossierPreviewRunFormat.Default.FontSizePx
            : paragraph.Runs.Max(run => run.Format.FontSizePx);
        var lineHeight = paragraph.Format.LineHeightPx ?? fontSize * 1.2;
        return paragraph.Format.SpaceBeforePx
            + lineHeight
            + paragraph.Format.SpaceAfterPx;
    }

    private static IReadOnlyList<IReadOnlyList<int>> FindOccurrences(
        IReadOnlyList<DossierOutputPreviewWord> words,
        string text)
    {
        var tokens = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(token => token.Length > 0)
            .ToList();
        if (tokens.Count == 0)
            return [];

        var normalizedWords = words
            .Select((word, index) => new IndexedWord(index, Normalize(word.Text)))
            .Where(word => word.Token.Length > 0)
            .ToList();
        var result = new List<IReadOnlyList<int>>();
        for (var start = 0; start <= normalizedWords.Count - tokens.Count; start++)
        {
            if (tokens.Select((token, offset) => string.Equals(
                    normalizedWords[start + offset].Token,
                    token,
                    StringComparison.Ordinal)).All(equal => equal))
            {
                result.Add(Enumerable.Range(0, tokens.Count)
                    .Select(offset => normalizedWords[start + offset].Index)
                    .ToList());
            }
        }

        return result;
    }

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

    private sealed record Rule(
        string AnchorText,
        string FieldKey,
        AreaKind Kind,
        IReadOnlyList<string>? TrailingTexts = null)
    {
        public IReadOnlyList<string> RequiredTrailingTexts => TrailingTexts ?? [];
    }

    private sealed record Candidate(
        DossierPreviewTable Table,
        DossierPreviewTableRow Row,
        DossierPreviewTableCell AnchorCell,
        DossierPreviewTableCell TargetCell);

    private readonly record struct Bounds(
        double Left,
        double Bottom,
        double Right,
        double Top);

    private readonly record struct IndexedWord(int Index, string Token);

    private enum AreaKind
    {
        WholeCell,
        FirstParagraph
    }
}
