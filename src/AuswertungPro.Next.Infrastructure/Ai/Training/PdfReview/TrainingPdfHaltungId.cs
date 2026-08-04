using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

/// <summary>
/// Behandelt Haltungs-IDs fuer die fachliche Speicherung.
/// Die Eval-Normalisierung darf hier nicht verwendet werden, weil sie echte
/// Punktbestandteile einer Haltungs-ID absichtlich zusammenfasst.
/// </summary>
internal static partial class TrainingPdfHaltungId
{
    [GeneratedRegex(@"\d[\d.]*[-/]\d[\d.]*")]
    private static partial Regex HaltungIdRegex();

    public static string? Extract(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = HaltungIdRegex().Match(value);
        return match.Success
            ? NormalizeForStorage(match.Value)
            : null;
    }

    /// <summary>
    /// Liest eine Haltungs-ID aus einem PDF-Dateinamen. Wenn der Dateiname die
    /// bereits aus dem Elternordner bekannte ID exakt enthaelt, gewinnt diese
    /// nur bei sauberer Trennung oder einem plausiblen yyyyMM/yyyyMMdd-Praefix.
    /// So wird z. B. "20240461721-61720.pdf" nicht zu
    /// "20240461721-61720" zusammengezogen.
    /// </summary>
    public static string? ExtractFromFileName(
        string? fileNameWithoutExtension,
        string? folderHaltungId)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return null;

        var normalizedFolder = NormalizeForStorage(folderHaltungId);
        if (normalizedFolder is not null)
        {
            var normalizedName = fileNameWithoutExtension.Replace('/', '-');
            var compactDateCandidate =
                ExtractAfterCompactDate(normalizedName);
            if (compactDateCandidate is not null
                && AreEquivalent(
                    compactDateCandidate,
                    normalizedFolder))
            {
                return PreferCleanAlias(
                    compactDateCandidate,
                    normalizedFolder);
            }

            var index = normalizedName.LastIndexOf(
                normalizedFolder,
                StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var candidateMatch = HaltungIdRegex().Match(
                    normalizedName,
                    index);
                if (!candidateMatch.Success
                    || candidateMatch.Index != index
                    || !AreEquivalent(
                        candidateMatch.Value,
                        normalizedFolder))
                {
                    return Extract(fileNameWithoutExtension);
                }

                var prefix = normalizedName[..candidateMatch.Index];
                var suffix = normalizedName[
                    (candidateMatch.Index + candidateMatch.Length)..];
                var cleanLeftBoundary = prefix.Length == 0
                                        || !IsHaltungEndpointCharacter(prefix[^1])
                                        || IsPlausibleCompactDate(prefix);
                var cleanRightBoundary = suffix.Length == 0
                                         || !IsHaltungEndpointCharacter(suffix[0]);
                if (cleanLeftBoundary && cleanRightBoundary)
                    return normalizedFolder;
            }
        }

        return Extract(fileNameWithoutExtension);
    }

    private static string? ExtractAfterCompactDate(string normalizedName)
    {
        foreach (var dateLength in new[] { 8, 6 })
        {
            if (normalizedName.Length <= dateLength
                || !IsPlausibleCompactDate(
                    normalizedName[..dateLength]))
            {
                continue;
            }

            var candidateStart = dateLength;
            while (candidateStart < normalizedName.Length
                   && normalizedName[candidateStart] is '_' or '-' or ' ')
            {
                candidateStart++;
            }

            var match = HaltungIdRegex().Match(
                normalizedName,
                candidateStart);
            if (!match.Success || match.Index != candidateStart)
                continue;

            var suffixStart = match.Index + match.Length;
            if (suffixStart < normalizedName.Length
                && IsHaltungEndpointCharacter(
                    normalizedName[suffixStart]))
            {
                continue;
            }

            return NormalizeForStorage(match.Value);
        }

        return null;
    }

    public static string? NormalizeForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = HaltungIdRegex().Match(value);
        if (!match.Success)
            return null;

        return match.Value
            .Replace('/', '-')
            .Trim('.');
    }

    public static bool AreEquivalent(string? left, string? right)
    {
        var leftKey = CreateComparisonKey(left);
        var rightKey = CreateComparisonKey(right);
        return leftKey is not null
               && rightKey is not null
               && string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    public static string? PreferCleanAlias(string? first, string? second)
    {
        var normalizedFirst = NormalizeForStorage(first);
        var normalizedSecond = NormalizeForStorage(second);
        if (normalizedFirst is null)
            return normalizedSecond;
        if (normalizedSecond is null)
            return normalizedFirst;

        var firstHasTerminalZero = HasTerminalZeroSuffix(normalizedFirst);
        var secondHasTerminalZero = HasTerminalZeroSuffix(normalizedSecond);
        if (firstHasTerminalZero != secondHasTerminalZero)
        {
            return firstHasTerminalZero
                ? normalizedSecond
                : normalizedFirst;
        }

        return normalizedFirst;
    }

    private static string? CreateComparisonKey(string? value)
    {
        var normalized = NormalizeForStorage(value);
        if (normalized is null)
            return null;

        var endpoints = normalized.Split('-', StringSplitOptions.TrimEntries);
        if (endpoints.Length != 2)
            return normalized;

        return string.Join(
            '-',
            endpoints.Select(RemoveTerminalZeroSuffix));
    }

    private static bool HasTerminalZeroSuffix(string value)
        => value.Split('-', StringSplitOptions.TrimEntries)
            .Any(endpoint => endpoint.EndsWith(".0", StringComparison.Ordinal));

    private static string RemoveTerminalZeroSuffix(string endpoint)
        => endpoint.EndsWith(".0", StringComparison.Ordinal)
            ? endpoint[..^2]
            : endpoint;

    private static bool IsHaltungEndpointCharacter(char value)
        => char.IsDigit(value) || value == '.';

    private static bool IsPlausibleCompactDate(string prefix)
    {
        var compact = prefix.TrimEnd('_', '-', ' ');
        if (compact.Length is not (6 or 8)
            || !compact.All(char.IsDigit)
            || !int.TryParse(compact.AsSpan(0, 4), out var year)
            || year is < 1900 or > 2200
            || !int.TryParse(compact.AsSpan(4, 2), out var month)
            || month is < 1 or > 12)
        {
            return false;
        }

        if (compact.Length == 6)
            return true;

        return int.TryParse(compact.AsSpan(6, 2), out var day)
               && day >= 1
               && day <= DateTime.DaysInMonth(year, month);
    }
}
