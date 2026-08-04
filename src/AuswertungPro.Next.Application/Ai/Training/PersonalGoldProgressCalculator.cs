using System.IO;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Berechnet den persoenlichen Goldstand je Hauptcode ohne Daten zu veraendern.
/// Nur vollstaendige Handlabels mit Box und Segmentierung zaehlen als Goldframe.
/// </summary>
public static class PersonalGoldProgressCalculator
{
    public static IReadOnlyList<PersonalGoldMainCodeStatus> Calculate(
        IEnumerable<TrainingSample> samples,
        string confirmedByUser,
        IReadOnlyList<string>? requiredMainCodes = null,
        int targetMinimum = 30,
        int targetMaximum = 50,
        Func<string, bool>? frameIsReadable = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedByUser);
        if (targetMinimum <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetMinimum));
        if (targetMaximum < targetMinimum)
            throw new ArgumentOutOfRangeException(nameof(targetMaximum));

        frameIsReadable ??= CanReadFrame;
        var selected = samples
            // Entwuerfe (Status=Draft) zaehlen als persoenliche, noch unvollstaendige Samples —
            // sie tauchen so im Goldstand als "unvollstaendig" auf, nie aber als vollstaendig
            // (HasBbox && HasSamMask fehlt ihnen per Konstruktion).
            .Where(sample =>
                ManualGoldTrainingPolicy.IsPersonallyReviewed(sample, confirmedByUser)
                || GoldDraftMatcher.IsOwnDraft(sample, confirmedByUser))
            .ToArray();
        var required = (requiredMainCodes ?? PersonalGoldMainCodeCatalog.RequiredCodes)
            .Select(NormalizeMainCode)
            .Where(code => code is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in selected.Select(sample => NormalizeMainCode(sample.Code)).Where(code => code is not null))
            required.Add(code!);

        return required
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(code => BuildStatus(
                code,
                selected,
                confirmedByUser,
                frameIsReadable,
                targetMinimum,
                targetMaximum))
            .ToArray();
    }

    private static PersonalGoldMainCodeStatus BuildStatus(
        string mainCode,
        IReadOnlyList<TrainingSample> selected,
        string confirmedByUser,
        Func<string, bool> frameIsReadable,
        int targetMinimum,
        int targetMaximum)
    {
        var codeSamples = selected
            .Where(sample => string.Equals(
                NormalizeMainCode(sample.Code),
                mainCode,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var full = codeSamples
            .Where(sample =>
                ManualGoldTrainingPolicy
                    .EvaluateForExport(sample, confirmedByUser)
                    .IsEligible)
            .Where(sample => IsReadableFrame(sample.FramePath, frameIsReadable))
            .ToArray();
        var uniqueFrames = full
            .Select(sample => sample.FramePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var status = uniqueFrames == 0
            ? "missing"
            : uniqueFrames < targetMinimum
                ? "needs_more"
                : uniqueFrames <= targetMaximum
                    ? "ready"
                    : "above_target";

        return new PersonalGoldMainCodeStatus(
            mainCode,
            codeSamples.Length,
            codeSamples.Count(sample => sample.HasBbox),
            full.Length,
            uniqueFrames,
            targetMinimum,
            targetMaximum,
            Math.Max(0, targetMinimum - uniqueFrames),
            status);
    }

    private static bool IsReadableFrame(
        string? framePath,
        Func<string, bool> frameIsReadable)
    {
        if (string.IsNullOrWhiteSpace(framePath))
            return false;

        try
        {
            return frameIsReadable(framePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool CanReadFrame(string framePath)
    {
        try
        {
            using var stream = new FileStream(
                framePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.Length > 0 && stream.ReadByte() >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeMainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().Replace(".", string.Empty).ToUpperInvariant();
        return normalized.Length >= 3 ? normalized[..3] : null;
    }
}
