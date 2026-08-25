using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>Ein sichtbarer Text und das Feld, das ihn bearbeitet.</summary>
public sealed record DossierPreviewTextCandidate(
    DossierPreviewTarget Target,
    string Text);

/// <summary>
/// Verbindet die echten Wörter der PDF-Seite mit ihren Eingabefeldern. Dadurch
/// bleibt der direkte Klick erhalten, obwohl das Blatt nicht mehr nachgezeichnet
/// wird, sondern als echte Ausgabeseite erscheint.
/// </summary>
public static class DossierOutputPreviewHitMatcher
{
    public static IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> Match(
        IReadOnlyList<DossierOutputPreviewWord>? words,
        IEnumerable<DossierPreviewTextCandidate>? candidates)
    {
        if (words is null || words.Count == 0 || candidates is null)
            return new Dictionary<int, IReadOnlyList<DossierPreviewTarget>>();

        var normalizedWords = words
            .Select((word, index) => new IndexedWord(index, NormalizeToken(word.Text)))
            .Where(word => word.Token.Length > 0)
            .ToList();
        var matches = new Dictionary<int, List<DossierPreviewTarget>>();

        foreach (var candidate in candidates
                     .Select(candidate => new NormalizedCandidate(
                         candidate.Target,
                         Tokens(candidate.Text)))
                     .Where(candidate => candidate.Tokens.Count > 0)
                     .OrderByDescending(candidate => candidate.Tokens.Count)
                     .ThenByDescending(candidate => candidate.Tokens.Sum(token => token.Length))
                     .DistinctBy(candidate => (candidate.Target, string.Join("\0", candidate.Tokens))))
        {
            for (var start = 0; start <= normalizedWords.Count - candidate.Tokens.Count; start++)
            {
                if (!MatchesAt(normalizedWords, candidate.Tokens, start))
                    continue;

                for (var offset = 0; offset < candidate.Tokens.Count; offset++)
                {
                    var wordIndex = normalizedWords[start + offset].Index;
                    if (!matches.TryGetValue(wordIndex, out var targets))
                        matches[wordIndex] = targets = new List<DossierPreviewTarget>();

                    if (!targets.Contains(candidate.Target))
                        targets.Add(candidate.Target);
                }
            }
        }

        return matches.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<DossierPreviewTarget>)pair.Value);
    }

    private static bool MatchesAt(
        IReadOnlyList<IndexedWord> words,
        IReadOnlyList<string> tokens,
        int start)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!string.Equals(
                    words[start + i].Token,
                    tokens[i],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> Tokens(string? text)
        => (text ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .Where(token => token.Length > 0)
            .ToList();

    private static string NormalizeToken(string? text)
        => new((text ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private sealed record IndexedWord(int Index, string Token);

    private sealed record NormalizedCandidate(
        DossierPreviewTarget Target,
        IReadOnlyList<string> Tokens);
}
