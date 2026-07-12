using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal sealed record PdfTextReplacementTarget(string SearchText, string ReplacementText);

internal sealed record PdfTextReplacementMatch(
    PdfTextReplacementTarget Replacement,
    int StartLetterIndex,
    int EndLetterIndex,
    double Left,
    double Bottom,
    double Right,
    double Top,
    PdfPoint StartBaseLine,
    double FontSize);

/// <summary>
/// Findet vollstaendige Text-Tokens im PDF-Text-Layer und liefert deren Position.
/// Das Schreiben der PDF bleibt bewusst ausserhalb dieser Klasse.
/// </summary>
internal static class PdfTextReplacementMatcher
{
    internal static IReadOnlyList<PdfTextReplacementMatch> FindMatches(
        Page page,
        IReadOnlyList<PdfTextReplacementTarget> replacements)
    {
        var letters = page.Letters?.ToList() ?? new List<Letter>();
        if (letters.Count == 0 || replacements.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var flatTextBuilder = new StringBuilder();
        var charToLetterIndex = new List<int>(letters.Count * 2);
        for (var i = 0; i < letters.Count; i++)
        {
            var value = letters[i].Value;
            if (string.IsNullOrEmpty(value))
                continue;

            flatTextBuilder.Append(value);
            for (var c = 0; c < value.Length; c++)
                charToLetterIndex.Add(i);
        }

        var flatText = flatTextBuilder.ToString();
        if (flatText.Length == 0 || charToLetterIndex.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var matches = new List<PdfTextReplacementMatch>();
        foreach (var replacement in replacements)
        {
            var search = replacement.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length > flatText.Length)
                continue;

            var searchStart = 0;
            while (searchStart <= flatText.Length - search.Length)
            {
                var foundIndex = flatText.IndexOf(search, searchStart, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    break;

                if (IsReplacementBoundary(flatText, foundIndex, search.Length))
                {
                    var foundEnd = foundIndex + search.Length - 1;
                    if (foundEnd >= 0 && foundEnd < charToLetterIndex.Count)
                    {
                        var startLetterIndex = charToLetterIndex[foundIndex];
                        var endLetterIndex = charToLetterIndex[foundEnd];
                        var match = TryBuildMatch(letters, replacement, startLetterIndex, endLetterIndex);
                        if (match is not null)
                            matches.Add(match);
                    }
                }

                searchStart = foundIndex + search.Length;
            }
        }

        return matches.Count <= 1 ? matches : FilterOverlappingMatches(matches);
    }

    private static PdfTextReplacementMatch? TryBuildMatch(
        IReadOnlyList<Letter> letters,
        PdfTextReplacementTarget replacement,
        int startLetterIndex,
        int endLetterIndex)
    {
        if (startLetterIndex < 0 || endLetterIndex < startLetterIndex || endLetterIndex >= letters.Count)
            return null;
        if (!IsSpatiallyContinuous(letters, startLetterIndex, endLetterIndex))
            return null;

        var left = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MaxValue;
        var top = double.MinValue;
        double fontSizeSum = 0;
        var fontSizeCount = 0;
        var startBaseline = letters[startLetterIndex].StartBaseLine;

        for (var i = startLetterIndex; i <= endLetterIndex; i++)
        {
            var glyph = letters[i].GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
            bottom = Math.Min(bottom, glyph.Bottom);
            top = Math.Max(top, glyph.Top);

            if (letters[i].FontSize > 0)
            {
                fontSizeSum += letters[i].FontSize;
                fontSizeCount++;
            }
        }

        if (left == double.MaxValue || right == double.MinValue || bottom == double.MaxValue || top == double.MinValue)
            return null;

        var fontSize = fontSizeCount > 0 ? fontSizeSum / fontSizeCount : 9d;
        return new PdfTextReplacementMatch(
            replacement,
            startLetterIndex,
            endLetterIndex,
            left,
            bottom,
            right,
            top,
            startBaseline,
            fontSize);
    }

    private static bool IsSpatiallyContinuous(
        IReadOnlyList<Letter> letters,
        int startLetterIndex,
        int endLetterIndex)
    {
        for (var index = startLetterIndex + 1; index <= endLetterIndex; index++)
        {
            var previous = letters[index - 1];
            var current = letters[index];
            var fontSize = Math.Max(
                previous.FontSize > 0 ? previous.FontSize : 9d,
                current.FontSize > 0 ? current.FontSize : 9d);

            var baselineTolerance = Math.Max(1.5d, fontSize * 0.45d);
            if (Math.Abs(current.StartBaseLine.Y - previous.StartBaseLine.Y) > baselineTolerance)
                return false;

            var maxGap = Math.Max(3d, fontSize * 1.5d);
            var horizontalGap = current.GlyphRectangle.Left - previous.GlyphRectangle.Right;
            if (horizontalGap > maxGap)
                return false;

            if (current.GlyphRectangle.Right < previous.GlyphRectangle.Left - maxGap)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FilterOverlappingMatches(
        IReadOnlyList<PdfTextReplacementMatch> matches)
    {
        var accepted = new List<PdfTextReplacementMatch>();
        foreach (var candidate in matches
                     .OrderBy(m => m.StartLetterIndex)
                     .ThenByDescending(m => m.EndLetterIndex - m.StartLetterIndex))
        {
            var overlaps = accepted.Any(existing =>
                !(candidate.EndLetterIndex < existing.StartLetterIndex
                  || candidate.StartLetterIndex > existing.EndLetterIndex));
            if (!overlaps)
                accepted.Add(candidate);
        }

        return accepted;
    }

    private static bool IsReplacementBoundary(string text, int startIndex, int length)
    {
        if (startIndex < 0 || length <= 0 || startIndex + length > text.Length)
            return false;

        var before = startIndex > 0 ? text[startIndex - 1] : '\0';
        var afterIndex = startIndex + length;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsIdentifierCharacter(before) && !IsIdentifierCharacter(after);
    }

    private static bool IsIdentifierCharacter(char ch)
        => ch != '\0'
           && (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.');
}
