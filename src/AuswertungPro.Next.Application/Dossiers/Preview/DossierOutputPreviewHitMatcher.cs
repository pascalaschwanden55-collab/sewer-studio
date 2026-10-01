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
    private const int MinimumTokensForAnchorFallback = 6;
    private const int MaximumAnchorTokens = 5;
    private const int MinimumAnchorTokens = 4;
    private const string TokenSeparator = "\u001f";

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

        var kandidaten = candidates
            .Select(candidate => new NormalizedCandidate(
                candidate.Target,
                Tokens(candidate.Text)))
            .Where(candidate => candidate.Tokens.Count > 0)
            .OrderByDescending(candidate => candidate.Tokens.Count)
            .ThenByDescending(candidate => candidate.Tokens.Sum(token => token.Length))
            .DistinctBy(candidate => (candidate.Target, TokenKey(candidate.Tokens)))
            .ToList();

        var unterschiedlicheTexte = kandidaten
            .DistinctBy(candidate => TokenKey(candidate.Tokens))
            .Select(candidate => candidate.Tokens)
            .ToList();

        // Ein gleicher Wert kann in verschiedenen Feldern stehen — zum
        // Beispiel Haus-Nr. und Parzellen-Nr. „30". Die Reihenfolge des
        // Feldkatalogs ist kein geometrischer Beweis; solche verschiedenen
        // Stellen bleiben hier deshalb bewusst ohne Treffer. Nur echte
        // Geschwister derselben Wiederholspalte duerfen in Zeilenreihenfolge
        // verteilt werden. Verschiedene physische Tabellenzellen loest der
        // getrennte Tabellenmapper ueber ihre Spaltengeometrie auf.
        foreach (var gruppe in kandidaten.GroupBy(kandidat => TokenKey(kandidat.Tokens)))
        {
            var tokens = gruppe.First().Tokens;
            var fundstellen = ExakteFundstellen(normalizedWords, tokens);
            var partialFallback = false;

            // PDF-Bibliotheken trennen Wörter nicht immer gleich wie Word.
            // Beispiel: Aus „Kanalisationsleitungen" können die zwei PDF-
            // Wörter „Kanalisations" und „leitungen" werden. Die Buchstaben
            // bleiben gleich; deshalb ist dieser Rückfall weiterhin exakt und
            // verändert nur die Wortgrenzen.
            if (fundstellen.Count == 0
                && WortgrenzenVarianteIstEindeutig(tokens, unterschiedlicheTexte))
            {
                fundstellen = WortgrenzenUnabhaengigeFundstellen(
                    normalizedWords,
                    tokens);
            }

            // Bei langen Absätzen kann ein einzelnes abweichendes PDF-Wort die
            // vollständige Suche verhindern. Dann decken eindeutige 5- oder
            // 4-Wort-Anker die sicher zuordenbaren Teile ab. Allgemeine
            // Ähnlichkeitssuche gibt es
            // bewusst nicht: Zwei ähnliche Texte dürfen nie verwechselt werden.
            if (fundstellen.Count == 0
                && tokens.Count >= MinimumTokensForAnchorFallback)
            {
                var anker = EindeutigeAnkerFinden(
                    normalizedWords,
                    tokens,
                    unterschiedlicheTexte);

                fundstellen.AddRange(anker);
                partialFallback = anker.Count > 0;
            }

            // Tabellenzellen koennen im PDF-Inhaltsstrom mit den Woertern der
            // Nachbarspalte verschachtelt sein. Wenn selbst die sicheren
            // Mehrwort-Anker deshalb scheitern, markieren mehrere lange
            // Einzelwoerter die Zelle nur dann, wenn jedes davon sowohl auf
            // diesem Blatt als auch unter allen Feldtexten eindeutig ist.
            // Der Flaechenbauer spannt daraus anschliessend wieder die ganze
            // Tabellenzelle auf.
            if (fundstellen.Count == 0
                && gruppe.Any(kandidat => kandidat.Target.Kind is
                    DossierPreviewTargetKind.Row or DossierPreviewTargetKind.RowCell))
            {
                var cellWords = EindeutigeZellenWoerterFinden(
                    normalizedWords,
                    tokens,
                    unterschiedlicheTexte);
                fundstellen.AddRange(cellWords);
                partialFallback = cellWords.Count > 0;
            }

            var stellen = FachlicheStellen(gruppe.ToList());

            // Nur eine fachliche Stelle: Sie gehoert zu jeder Fundstelle. Eine
            // Ueberschrift darf durchaus mehrfach im Blatt stehen.
            if (stellen.Count == 1)
            {
                foreach (var fundstelle in fundstellen)
                {
                    foreach (var kandidat in stellen[0].Candidates)
                        Merke(matches, normalizedWords, kandidat, fundstelle);
                }

                continue;
            }

            // Teilanker beweisen nur die Zelle, nicht welche von mehreren
            // identischen Listenzeilen gemeint ist. Ihre Anzahl darf deshalb
            // niemals als vermeintliche Zeilenanzahl benutzt werden.
            if (partialFallback
                || !SindListengeschwister(stellen)
                || stellen.Count != fundstellen.Count)
                continue;

            for (var i = 0; i < stellen.Count; i++)
            {
                foreach (var kandidat in stellen[i].Candidates)
                    Merke(matches, normalizedWords, kandidat, fundstellen[i]);
            }
        }

        return matches.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<DossierPreviewTarget>)pair.Value);
    }

    /// <summary>
    /// Die Anfaenge aller Fundstellen dieser Wortfolge, in Leserichtung und
    /// ohne Ueberschneidung. Ohne die Ueberschneidungssperre wuerde eine Folge
    /// wie „30 30" zwei Fundstellen an benachbarten Stellen melden.
    /// </summary>
    private static List<Fundstelle> ExakteFundstellen(
        IReadOnlyList<IndexedWord> words,
        IReadOnlyList<string> tokens)
    {
        var ergebnis = new List<Fundstelle>();

        for (var start = 0; start <= words.Count - tokens.Count; start++)
        {
            if (!MatchesAt(words, tokens, start))
                continue;

            ergebnis.Add(new Fundstelle(start, tokens.Count));
            start += tokens.Count - 1;
        }

        return ergebnis;
    }

    private static List<Fundstelle> WortgrenzenUnabhaengigeFundstellen(
        IReadOnlyList<IndexedWord> words,
        IReadOnlyList<string> tokens,
        bool ueberschneidungenZulassen = false)
    {
        var ergebnis = new List<Fundstelle>();
        var suchtext = string.Concat(tokens);

        for (var start = 0; start < words.Count; start++)
        {
            var zeichen = 0;

            for (var ende = start; ende < words.Count; ende++)
            {
                var wort = words[ende].Token;
                if (zeichen + wort.Length > suchtext.Length
                    || !suchtext.AsSpan(zeichen, wort.Length).SequenceEqual(wort.AsSpan()))
                {
                    break;
                }

                zeichen += wort.Length;
                if (zeichen != suchtext.Length)
                    continue;

                ergebnis.Add(new Fundstelle(start, ende - start + 1));
                if (!ueberschneidungenZulassen)
                    start = ende;

                break;
            }
        }

        return ergebnis;
    }

    private static bool WortgrenzenVarianteIstEindeutig(
        IReadOnlyList<string> tokens,
        IReadOnlyList<IReadOnlyList<string>> unterschiedlicheTexte)
    {
        var buchstaben = string.Concat(tokens);
        return unterschiedlicheTexte.Count(text =>
            string.Equals(
                string.Concat(text),
                buchstaben,
                StringComparison.Ordinal)) == 1;
    }

    private static IReadOnlyList<Fundstelle> EindeutigeAnkerFinden(
        IReadOnlyList<IndexedWord> words,
        IReadOnlyList<string> kandidat,
        IReadOnlyList<IReadOnlyList<string>> unterschiedlicheTexte)
    {
        for (var laenge = MaximumAnchorTokens;
             laenge >= MinimumAnchorTokens;
             laenge--)
        {
            var moeglicheAnker = new List<(Fundstelle Fundstelle, int Zeichen, int Start)>();

            for (var start = 0; start <= kandidat.Count - laenge; start++)
            {
                var anker = kandidat.Skip(start).Take(laenge).ToList();
                if (!AnkerIstUnterKandidatenEindeutig(anker, unterschiedlicheTexte))
                    continue;

                var fundstellen = WortgrenzenUnabhaengigeFundstellen(
                    words,
                    anker,
                    ueberschneidungenZulassen: true);
                if (fundstellen.Count != 1)
                    continue;

                moeglicheAnker.Add((
                    fundstellen[0],
                    anker.Sum(token => token.Length),
                    start));
            }

            if (moeglicheAnker.Count > 0)
            {
                return moeglicheAnker
                    .OrderBy(anker => anker.Fundstelle.Start)
                    .ThenByDescending(anker => anker.Zeichen)
                    .Select(anker => anker.Fundstelle)
                    .Distinct()
                    .ToList();
            }
        }

        return [];
    }

    private static bool AnkerIstUnterKandidatenEindeutig(
        IReadOnlyList<string> anker,
        IReadOnlyList<IReadOnlyList<string>> unterschiedlicheTexte)
        => unterschiedlicheTexte.Count(text =>
            WortgrenzenUnabhaengigeFundstellen(
                text.Select((token, index) => new IndexedWord(index, token)).ToList(),
                anker).Count > 0) == 1;

    private static IReadOnlyList<Fundstelle> EindeutigeZellenWoerterFinden(
        IReadOnlyList<IndexedWord> words,
        IReadOnlyList<string> kandidat,
        IReadOnlyList<IReadOnlyList<string>> unterschiedlicheTexte)
    {
        var result = new List<Fundstelle>();

        foreach (var token in kandidat
                     .Where(token => token.Length >= 6)
                     .Distinct(StringComparer.Ordinal))
        {
            if (unterschiedlicheTexte.Count(text => text.Contains(token)) != 1)
                continue;

            var occurrences = words
                .Select((word, index) => (word, index))
                .Where(item => string.Equals(
                    item.word.Token,
                    token,
                    StringComparison.Ordinal))
                .Select(item => item.index)
                .ToList();

            if (occurrences.Count == 1)
                result.Add(new Fundstelle(occurrences[0], 1));
        }

        // Ein einzelnes allgemeines Wort ist kein belastbarer Beleg fuer eine
        // ganze Zelle. Zwei unabhaengige Marken sind die Untergrenze.
        return result.Count >= 2 ? result : [];
    }

    /// <summary>
    /// Eine Tabellenzeile und ihre genauere Zelle duerfen dieselbe Fundstelle
    /// gemeinsam tragen. Zwei verschiedene Felder oder zwei verschiedene
    /// Tabellenzellen sind dagegen zwei Stellen und werden nicht vermischt.
    /// </summary>
    private static IReadOnlyList<FachlicheStelle> FachlicheStellen(
        IReadOnlyList<NormalizedCandidate> kandidaten)
    {
        var result = new List<FachlicheStelle>();

        foreach (var kandidat in kandidaten)
        {
            var target = kandidat.Target;
            var companions = target.Kind == DossierPreviewTargetKind.Row
                ? kandidaten
                    .Where(other => other.Target.Kind == DossierPreviewTargetKind.RowCell
                        && string.Equals(
                            other.Target.Key,
                            target.Key,
                            StringComparison.OrdinalIgnoreCase)
                        && other.Target.RowIndex == target.RowIndex)
                    .Select(other => other.Target)
                    .Distinct()
                    .ToList()
                : [];
            var slotTarget = companions.Count == 1 ? companions[0] : target;

            var slot = result.FirstOrDefault(existing => existing.Address == slotTarget);

            if (slot is null)
            {
                slot = new FachlicheStelle(slotTarget, []);
                result.Add(slot);
            }

            if (!slot.Candidates.Contains(kandidat))
                slot.Candidates.Add(kandidat);
        }

        return result;
    }

    private static bool SindListengeschwister(
        IReadOnlyList<FachlicheStelle> stellen)
    {
        if (stellen.Count < 2)
            return true;

        var first = stellen[0].Address;
        if (first.Kind is not (DossierPreviewTargetKind.Row
            or DossierPreviewTargetKind.RowCell))
        {
            return false;
        }

        return stellen.All(stelle =>
            stelle.Address.Kind == first.Kind
            && string.Equals(
                stelle.Address.Key,
                first.Key,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                stelle.Address.CellKey,
                first.CellKey,
                StringComparison.OrdinalIgnoreCase))
            && stellen.Select(stelle => stelle.Address.RowIndex).Distinct().Count()
                == stellen.Count;
    }

    private static void Merke(
        Dictionary<int, List<DossierPreviewTarget>> matches,
        IReadOnlyList<IndexedWord> words,
        NormalizedCandidate kandidat,
        Fundstelle fundstelle)
    {
        for (var offset = 0; offset < fundstelle.WordCount; offset++)
        {
            var wordIndex = words[fundstelle.Start + offset].Index;
            if (!matches.TryGetValue(wordIndex, out var targets))
                matches[wordIndex] = targets = new List<DossierPreviewTarget>();

            if (!targets.Contains(kandidat.Target))
                targets.Add(kandidat.Target);
        }
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

    private static string TokenKey(IReadOnlyList<string> tokens)
        => string.Join(TokenSeparator, tokens);

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

    private sealed record FachlicheStelle(
        DossierPreviewTarget Address,
        List<NormalizedCandidate> Candidates);

    private sealed record Fundstelle(int Start, int WordCount);
}
