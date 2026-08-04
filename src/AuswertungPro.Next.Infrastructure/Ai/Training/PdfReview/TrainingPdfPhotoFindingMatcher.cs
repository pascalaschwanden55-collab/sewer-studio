using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

internal sealed record TrainingPdfMatchedFinding(
    string VsaCode,
    string Beschreibung,
    double MeterStart,
    double MeterEnd,
    bool IsStreckenschaden,
    string? PhotoId,
    string MatchKind);

internal sealed record TrainingPdfPhotoMatchResult(
    IReadOnlyList<TrainingPdfMatchedFinding> Findings,
    string? IssueCode,
    string? IssueMessage,
    string? PhotoId);

/// <summary>
/// Konservativer Foto-Befund-Matcher. Reihenfolge:
/// lokaler expliziter Code, Foto-ID/Dateiname, danach nur ein eindeutiger
/// exakter Zeit-/Meter-/Befundtreffer. Freie Text-zu-Code-Raterei ist verboten.
/// </summary>
internal static partial class TrainingPdfPhotoFindingMatcher
{
    private sealed record EntryResolution(
        GroundTruthEntry? Entry,
        bool IsAmbiguous);

    private sealed record PhotoTokenResolution(
        IReadOnlyList<GroundTruthEntry> Entries,
        bool IsAmbiguous);

    private readonly record struct EntryIdentity(
        string Code,
        double MeterStart,
        double MeterEnd,
        long? TimeTicks,
        string Text,
        bool IsStreckenschaden);

    [GeneratedRegex(
        @"(?:Zustand|Kode|Code)[ \t]*:?[ \t]*(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DirectCodeRegex();

    [GeneratedRegex(
        @"(?<file>[A-Z0-9][A-Z0-9_.\-]{2,}\.(?:JPE?G|PNG))",
        RegexOptions.IgnoreCase)]
    private static partial Regex PhotoFileRegex();

    [GeneratedRegex(
        @"\bFoto\b[ \t]*:?[ \t]*(?<id>[A-Z0-9][A-Z0-9_.\-]{1,})",
        RegexOptions.IgnoreCase)]
    private static partial Regex PhotoIdRegex();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<id>\d{1,5}(?:-\d{1,5}){2,}[A-Z])(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase)]
    private static partial Regex PhotoSequenceTokenRegex();

    [GeneratedRegex(
        @"(?<!\d)(?<meter>\d{1,4}[.,]\d{1,3})\s*m\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex MeterRegex();

    [GeneratedRegex(
        @"(?<!\d)(?<time>\d{2}:\d{2}:\d{2})(?!\d)")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(
        @"(?<!\d)\d{2}:\d{2}:\d{2}[ \t]*[,;][ \t]*(?<meter>\d{1,4}[.,]\d{1,3})(?:[ \t]*m\b)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex CaptionMeterRegex();

    [GeneratedRegex(@"\b[A-Z]{2,6}(?:\.[A-Z]{1,2})*\b")]
    private static partial Regex CodeTokenRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWordRegex();

    private static readonly string[] NoisePrefixes =
    [
        "haltungsbildbericht",
        "haltungsbilder",
        "leitungsbildbericht",
        "kanalfernsehfotos",
        "haltung ",
        "leitung ",
        "oberer schacht",
        "unterer schacht",
        "oberer punkt",
        "unterer punkt",
        "insp.datum",
        "dimension",
        "nutzungsart",
        "strasse",
        "straße",
        "datenträger",
        "position",
        "zustand",
        "entf.",
        "video",
        "foto",
        "gedruckt am",
        "seite ",
    ];

    public static TrainingPdfPhotoMatchResult Match(
        TrainingPdfEmbeddedPhoto photo,
        IReadOnlyList<GroundTruthEntry> protocolEntries,
        string documentText,
        TrainingPdfProtocolMetadata? protocolMetadata = null)
    {
        var context = photo.ContextText ?? string.Empty;
        var photoId = ExtractPhotoTokens(context).FirstOrDefault();
        var meter = ParsePreferredMeter(context);
        var time = ParsePreferredTime(context);
        var metadata = protocolMetadata
                       ?? TrainingPdfProtocolMetadataParser.Parse(documentText);

        var directCodes = DirectCodeRegex()
            .Matches(context)
            .Select(match => NormalizeKnownCode(match.Groups["code"].Value))
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (directCodes.Length > 0)
        {
            var direct = new List<TrainingPdfMatchedFinding>(directCodes.Length);
            foreach (var code in directCodes)
            {
                var resolution = ResolveEntryForCode(
                    protocolEntries,
                    code,
                    meter,
                    time,
                    context,
                    []);
                if (resolution.IsAmbiguous)
                    return Ambiguous(photoId);

                direct.Add(BuildFinding(
                    code,
                    resolution.Entry,
                    context,
                    meter,
                    time,
                    photoId,
                    "same_block",
                    metadata));
            }

            return new TrainingPdfPhotoMatchResult(direct, null, null, photoId);
        }

        var tokenResolution = MatchByPhotoToken(
            context,
            protocolEntries,
            documentText,
            meter,
            time);
        if (tokenResolution.IsAmbiguous)
            return Ambiguous(photoId);
        if (tokenResolution.Entries.Count > 0)
        {
            return new TrainingPdfPhotoMatchResult(
                tokenResolution.Entries
                    .Select(entry => BuildFinding(
                        entry.VsaCode,
                        entry,
                        context,
                        meter,
                        time,
                        photoId,
                        "photo_id",
                        metadata))
                    .ToArray(),
                null,
                null,
                photoId);
        }

        var fallback = MatchByExactAnchors(context, protocolEntries, meter, time);
        if (fallback.Count == 1)
        {
            var entry = fallback[0];
            return new TrainingPdfPhotoMatchResult(
                [
                    BuildFinding(
                        entry.VsaCode,
                        entry,
                        context,
                        meter,
                        time,
                        photoId,
                        "time_meter_text",
                        metadata)
                ],
                null,
                null,
                photoId);
        }

        if (fallback.Count > 1)
        {
            return new TrainingPdfPhotoMatchResult(
                [],
                "ambiguous",
                "Foto passt zu mehreren Operateurbefunden und wurde nicht importiert.",
                photoId);
        }

        return new TrainingPdfPhotoMatchResult(
            [],
            "unmatched",
            "Kein eindeutiger gültiger Operateur-Code zum Foto gefunden.",
            photoId);
    }

    private static TrainingPdfPhotoMatchResult Ambiguous(string? photoId)
        => new(
            [],
            "ambiguous",
            "Foto passt zu mehreren Operateurbefunden und wurde nicht importiert.",
            photoId);

    private static PhotoTokenResolution MatchByPhotoToken(
        string context,
        IReadOnlyList<GroundTruthEntry> entries,
        string documentText,
        double? meter,
        TimeSpan? time)
    {
        var tokens = ExtractPhotoTokens(context);
        if (tokens.Count == 0)
            return new PhotoTokenResolution([], false);

        var lines = documentText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var matchedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codeSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tokenRows = new List<string>();
        foreach (var token in tokens)
        {
            var tokenLines = FindTokenLines(lines, token);
            foreach (var lineIndex in tokenLines)
            {
                tokenRows.Add(lines[lineIndex]);
                var rowCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var snippet = lines[lineIndex];
                foreach (Match codeMatch in CodeTokenRegex().Matches(snippet))
                {
                    var code = NormalizeKnownCode(codeMatch.Value);
                    if (code is null)
                        continue;

                    matchedCodes.Add(code);
                    rowCodes.Add(code);
                }

                if (rowCodes.Count > 0)
                {
                    codeSets.Add(string.Join(
                        '|',
                        rowCodes.Order(StringComparer.OrdinalIgnoreCase)));
                }
            }
        }

        // Dieselbe Foto-ID darf nicht auf verschiedene Codezeilen zeigen.
        // Eine reine Fotozeile ohne Code ist dagegen nur die Beschriftung.
        if (codeSets.Count > 1)
            return new PhotoTokenResolution([], true);

        if (matchedCodes.Count == 0)
        {
            var exactFindingCandidates = entries
                .Where(entry => IsMeaningfulDescription(entry.Text, entry.VsaCode))
                .Where(entry => tokenRows.Any(row =>
                    ContainsNormalizedFinding(row, entry.Text)))
                .GroupBy(CreateEntryIdentity)
                .Select(group => group.First())
                .ToArray();
            if (exactFindingCandidates.Length == 0)
                return new PhotoTokenResolution([], false);

            var localTextMatches = exactFindingCandidates
                .Where(entry => HasExactNormalizedFinding(context, entry.Text))
                .ToArray();
            if (localTextMatches.Length == 1)
                return new PhotoTokenResolution(localTextMatches, false);
            if (localTextMatches.Length > 1)
                return new PhotoTokenResolution([], true);

            var codeGroups = exactFindingCandidates
                .GroupBy(entry => entry.VsaCode, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (codeGroups.Length != 1)
                return new PhotoTokenResolution([], true);

            var resolution = ResolveEntryForCode(
                codeGroups[0].ToArray(),
                codeGroups[0].Key,
                meter,
                time,
                context,
                tokenRows);
            return resolution.Entry is not null
                ? new PhotoTokenResolution([resolution.Entry], false)
                : new PhotoTokenResolution([], resolution.IsAmbiguous);
        }

        var candidates = new List<GroundTruthEntry>(matchedCodes.Count);
        foreach (var code in matchedCodes.Order(StringComparer.OrdinalIgnoreCase))
        {
            var resolution = ResolveEntryForCode(
                entries,
                code,
                meter,
                time,
                context,
                tokenRows);
            if (resolution.IsAmbiguous)
                return new PhotoTokenResolution([], true);
            if (resolution.Entry is not null)
                candidates.Add(resolution.Entry);
        }

        return new PhotoTokenResolution(candidates, false);
    }

    private static int TokenAnchorScore(
        GroundTruthEntry entry,
        double? meter,
        TimeSpan? time)
    {
        var score = 0;
        if (meter.HasValue && Math.Abs(entry.MeterStart - meter.Value) <= 0.05)
            score += 2;
        if (time.HasValue
            && entry.Zeit.HasValue
            && Math.Abs((entry.Zeit.Value - time.Value).TotalSeconds) < 0.5)
        {
            score += 2;
        }

        return score;
    }

    private static IReadOnlyList<int> FindTokenLines(string[] lines, string token)
    {
        var normalizedToken = NormalizeToken(token);
        if (normalizedToken.Length < 3)
            return [];

        var result = new List<int>();
        for (var index = 0; index < lines.Length; index++)
        {
            var exactNumericPhoto = token.All(char.IsDigit)
                && Regex.IsMatch(
                    lines[index],
                    $@"\bFoto\s*:?\s*{Regex.Escape(token)}\b",
                    RegexOptions.IgnoreCase);
            var exactToken = Regex.IsMatch(
                lines[index],
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(token)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase);
            if (exactNumericPhoto || exactToken)
                result.Add(index);
        }

        return result;
    }

    private static IReadOnlyList<GroundTruthEntry> MatchByExactAnchors(
        string context,
        IReadOnlyList<GroundTruthEntry> entries,
        double? meter,
        TimeSpan? time)
    {
        // Der Ersatzschluessel ist absichtlich streng: Videozeit UND Meter
        // muessen exakt passen und der normalisierte Operateurbefund muss
        // vollstaendig im Fotoblock stehen. Eine Textaehnlichkeit darf hier
        // niemals einen Code erraten.
        if (!meter.HasValue || !time.HasValue)
            return [];

        return entries
            .Where(entry => Math.Abs(entry.MeterStart - meter.Value) <= 0.05)
            .Where(entry => entry.Zeit.HasValue
                            && Math.Abs((entry.Zeit.Value - time.Value).TotalSeconds) < 0.5)
            .Where(entry => HasExactNormalizedFinding(context, entry.Text))
            .GroupBy(CreateEntryIdentity)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool HasExactNormalizedFinding(string context, string? finding)
    {
        if (string.IsNullOrWhiteSpace(finding))
            return false;

        var normalizedFinding = NormalizeFindingText(finding);
        if (normalizedFinding.Length < 4)
            return false;

        return context
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(NormalizeFindingText)
            .Any(line => string.Equals(
                line,
                normalizedFinding,
                StringComparison.Ordinal));
    }

    private static bool ContainsNormalizedFinding(string line, string finding)
    {
        var normalizedLine = $" {NormalizeFindingText(line)} ";
        var normalizedFinding = NormalizeFindingText(finding);
        return normalizedFinding.Length >= 4
               && normalizedLine.Contains(
                   $" {normalizedFinding} ",
                   StringComparison.Ordinal);
    }

    private static string NormalizeFindingText(string value)
        => string.Join(
            ' ',
            NonWordRegex()
                .Split(value.ToLowerInvariant())
                .Where(token => token.Length > 0));

    private static EntryResolution ResolveEntryForCode(
        IReadOnlyList<GroundTruthEntry> entries,
        string code,
        double? meter,
        TimeSpan? time,
        string context,
        IReadOnlyList<string> evidenceRows)
    {
        var candidates = entries
            .Where(entry => string.Equals(entry.VsaCode, code, StringComparison.OrdinalIgnoreCase))
            .GroupBy(CreateEntryIdentity)
            .Select(group => group.First())
            .ToArray();
        if (candidates.Length == 0)
            return new EntryResolution(null, false);

        var plausible = candidates
            .Where(entry => !meter.HasValue || Math.Abs(entry.MeterStart - meter.Value) <= 0.05)
            .Where(entry => !time.HasValue
                            || !entry.Zeit.HasValue
                            || Math.Abs((entry.Zeit.Value - time.Value).TotalSeconds) < 0.5)
            .ToArray();
        if (plausible.Length == 0)
            return new EntryResolution(null, false);
        if (plausible.Length == 1)
            return new EntryResolution(plausible[0], false);

        var localTextMatches = plausible
            .Where(entry => HasExactNormalizedFinding(context, entry.Text))
            .ToArray();
        if (localTextMatches.Length == 1)
            return new EntryResolution(localTextMatches[0], false);
        if (localTextMatches.Length > 1)
            plausible = localTextMatches;

        var rowTextMatches = plausible
            .Where(entry => evidenceRows.Any(row =>
                ContainsNormalizedFinding(row, entry.Text)))
            .ToArray();
        if (rowTextMatches.Length == 1)
            return new EntryResolution(rowTextMatches[0], false);
        if (rowTextMatches.Length > 1)
            plausible = rowTextMatches;

        var ranked = plausible
            .Select(entry => new
            {
                Entry = entry,
                Score = TokenAnchorScore(entry, meter, time),
            })
            .ToArray();
        var maximumScore = ranked.Max(candidate => candidate.Score);
        var strongest = ranked
            .Where(candidate => candidate.Score == maximumScore)
            .ToArray();
        if (maximumScore > 0 && strongest.Length == 1)
            return new EntryResolution(strongest[0].Entry, false);

        return new EntryResolution(null, true);
    }

    private static EntryIdentity CreateEntryIdentity(GroundTruthEntry entry)
        => new(
            entry.VsaCode.ToUpperInvariant(),
            entry.MeterStart,
            entry.MeterEnd,
            entry.Zeit?.Ticks,
            NormalizeFindingText(entry.Text),
            entry.IsStreckenschaden);

    private static TrainingPdfMatchedFinding BuildFinding(
        string code,
        GroundTruthEntry? entry,
        string context,
        double? parsedMeter,
        TimeSpan? parsedTime,
        string? photoId,
        string matchKind,
        TrainingPdfProtocolMetadata metadata)
    {
        var detailed = metadata.FindFinding(
            code,
            entry?.MeterStart ?? parsedMeter,
            entry?.Zeit ?? parsedTime);
        string description;
        if (detailed is not null
            && IsMeaningfulDescription(detailed.Description, code))
        {
            description = detailed.Description.Trim();
        }
        else if (entry is not null
                 && IsMeaningfulDescription(entry.Text, code))
        {
            description = entry.Text.Trim();
        }
        else
        {
            description = ExtractDescription(context, code);
        }

        if (description.Length < 10)
            description = $"{code} — {description} (PDF-Operateurbefund)".Trim();

        var meterStart = detailed?.MeterStart
                         ?? entry?.MeterStart
                         ?? parsedMeter
                         ?? 0;
        var meterEnd = detailed?.MeterEnd
                       ?? entry?.MeterEnd
                       ?? meterStart;
        return new TrainingPdfMatchedFinding(
            code,
            description,
            meterStart,
            Math.Max(meterStart, meterEnd),
            detailed?.IsStreckenschaden == true
            || entry?.IsStreckenschaden == true,
            photoId,
            matchKind);
    }

    private static string ExtractDescription(string context, string code)
    {
        var candidates = context
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
            .Where(line => line.Length >= 3)
            .Where(line => !NoisePrefixes.Any(prefix =>
                line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Where(line => !string.Equals(
                NormalizeKnownCode(line),
                code,
                StringComparison.OrdinalIgnoreCase))
            .Where(line => !PhotoFileRegex().IsMatch(line))
            .Where(line => !Regex.IsMatch(line, @"^[\d\s.,:/\-]+$"))
            .OrderByDescending(DescriptionScore)
            .ToArray();

        return candidates.FirstOrDefault() ?? code;
    }

    private static int DescriptionScore(string line)
    {
        var score = Math.Min(line.Length, 160);
        if (line.Contains(',') || line.Contains(" Uhr", StringComparison.OrdinalIgnoreCase))
            score += 30;
        if (line.Contains("schacht", StringComparison.OrdinalIgnoreCase)
            || line.Contains("haltung", StringComparison.OrdinalIgnoreCase)
            || line.Contains("datum", StringComparison.OrdinalIgnoreCase))
        {
            score -= 80;
        }

        return score;
    }

    private static bool IsMeaningfulDescription(string? text, string code)
        => !string.IsNullOrWhiteSpace(text)
           && text.Trim().Length >= 3
           && !string.Equals(
               NormalizeKnownCode(text.Trim()),
               code,
               StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ExtractPhotoTokens(string context)
    {
        var tokens = new List<string>();
        tokens.AddRange(PhotoFileRegex()
            .Matches(context)
            .Select(match => match.Groups["file"].Value.Trim()));
        tokens.AddRange(PhotoIdRegex()
            .Matches(context)
            .Select(match => match.Groups["id"].Value.Trim().TrimEnd('.', ',', ';')));
        tokens.AddRange(PhotoSequenceTokenRegex()
            .Matches(context)
            .Select(match => match.Groups["id"].Value.Trim()));
        return tokens
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static double? ParsePreferredMeter(string text)
    {
        var preferred = Regex.Match(
            text,
            @"Entf[^\r\n\d]{0,40}(?<meter>\d{1,4}[.,]\d{1,3})\s*m",
            RegexOptions.IgnoreCase);
        var raw = preferred.Success
            ? preferred.Groups["meter"].Value
            : CaptionMeterRegex().IsMatch(text)
                ? CaptionMeterRegex().Match(text).Groups["meter"].Value
                : MeterRegex().Match(text).Groups["meter"].Value;
        return double.TryParse(
            raw.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static TimeSpan? ParsePreferredTime(string text)
    {
        var preferred = Regex.Match(
            text,
            @"Video[^\r\n\d]{0,20}(?<time>\d{2}:\d{2}:\d{2})",
            RegexOptions.IgnoreCase);
        var raw = preferred.Success
            ? preferred.Groups["time"].Value
            : TimeRegex().Match(text).Groups["time"].Value;
        return TimeSpan.TryParseExact(
            raw,
            @"hh\:mm\:ss",
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static string NormalizeToken(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string? NormalizeKnownCode(string? raw)
        => GroundTruthFieldParser.NormalizeKnownVsaCode(raw);
}
