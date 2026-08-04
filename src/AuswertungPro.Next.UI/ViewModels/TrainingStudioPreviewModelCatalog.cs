using AuswertungPro.Next.Application.Ai.Training.Preview;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Auswahl eines Modells, das das Foto nur zur Vorschau prueft.</summary>
public sealed record TrainingStudioPreviewModelOption(
    TrainingPreviewModelKind Kind,
    string DisplayName,
    string? CandidateId = null,
    string? CandidateSha256 = null);

/// <summary>Automatisch erkannte Vorschau-Box in echten Bildpixeln.</summary>
public sealed record TrainingStudioPreviewDetectionItem(
    double X1,
    double Y1,
    double X2,
    double Y2,
    string DisplayText,
    double Confidence);

/// <summary>
/// Reiner UI-Zustand fuer die pfadfreie BCC-Kandidatenauswahl.
/// </summary>
internal sealed record TrainingStudioPreviewModelCatalogState(
    IReadOnlyList<TrainingStudioPreviewModelOption> Options,
    TrainingStudioPreviewModelOption Selected,
    string? ErrorSummary);

/// <summary>
/// Baut die Modellliste fail-closed auf. Diese Klasse startet keine Inferenz
/// und liest oder schreibt keine Dateien.
/// </summary>
internal static class TrainingStudioPreviewModelCatalog
{
    public static TrainingStudioPreviewModelCatalogState Build(
        TrainingPreviewCandidateCatalogResult catalog,
        IReadOnlyList<TrainingStudioPreviewModelOption> currentOptions,
        TrainingStudioPreviewModelOption? currentSelection,
        bool standardModelUnavailable)
    {
        if (!catalog.Available || catalog.Candidates.Count == 0)
        {
            var summary = string.IsNullOrWhiteSpace(catalog.Error)
                ? "Keine manifest- und hashgeprueften BCC-Testkandidaten verfuegbar."
                : $"BCC-Kandidaten nicht verfuegbar: {catalog.Error}";
            return Unavailable(currentOptions, standardModelUnavailable, summary);
        }

        var activeOption = CreateActiveOption(currentOptions, standardModelUnavailable);
        var candidateOptions = catalog.Candidates
            .Where(candidate => HasExactCandidatePin(
                candidate.CandidateId,
                candidate.CandidateSha256))
            .Select(candidate => new TrainingStudioPreviewModelOption(
                TrainingPreviewModelKind.BccTestCandidate,
                BuildCandidateDisplayName(candidate),
                candidate.CandidateId,
                candidate.CandidateSha256))
            .ToArray();
        if (candidateOptions.Length == 0)
        {
            return Unavailable(
                currentOptions,
                standardModelUnavailable,
                "Keine sicher angehefteten BCC-Testkandidaten verfuegbar.");
        }

        var selected = currentSelection?.Kind switch
        {
            TrainingPreviewModelKind.BccTestCandidate
                => candidateOptions.FirstOrDefault(item =>
                    string.Equals(
                        item.CandidateId,
                        currentSelection.CandidateId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.CandidateSha256,
                        currentSelection.CandidateSha256,
                        StringComparison.OrdinalIgnoreCase))
                   ?? activeOption,
            _ => activeOption,
        };
        return new TrainingStudioPreviewModelCatalogState(
            [activeOption, .. candidateOptions],
            selected,
            ErrorSummary: null);
    }

    public static TrainingStudioPreviewModelCatalogState Unavailable(
        IReadOnlyList<TrainingStudioPreviewModelOption> currentOptions,
        bool standardModelUnavailable,
        string summary)
    {
        var activeOption = CreateActiveOption(currentOptions, standardModelUnavailable);
        return new TrainingStudioPreviewModelCatalogState(
            [activeOption],
            activeOption,
            summary);
    }

    public static TrainingStudioPreviewModelOption CreateActiveOption(
        IReadOnlyList<TrainingStudioPreviewModelOption> currentOptions,
        bool standardModelUnavailable)
    {
        var displayName = standardModelUnavailable
            ? "Aktives Standardmodell (nicht freigegeben)"
            : "Aktives Standardmodell";
        return currentOptions.FirstOrDefault(
                   item => item.Kind == TrainingPreviewModelKind.ActiveStandard
                           && string.Equals(
                               item.DisplayName,
                               displayName,
                               StringComparison.Ordinal))
               ?? new TrainingStudioPreviewModelOption(
                   TrainingPreviewModelKind.ActiveStandard,
                   displayName);
    }

    public static bool HasExactCandidatePin(
        string? candidateId,
        string? candidateSha256)
    {
        if (string.IsNullOrEmpty(candidateId)
            || candidateId.Length > 128
            || !char.IsAsciiLetterOrDigit(candidateId[0])
            || candidateSha256 is not { Length: 64 }
            || !candidateSha256.All(Uri.IsHexDigit))
        {
            return false;
        }

        return candidateId.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '_' or '-');
    }

    private static string BuildCandidateDisplayName(
        TrainingPreviewCandidateInfo candidate)
    {
        var shortSha = candidate.CandidateSha256[..12];
        return $"BCC-Testmodell · {candidate.CandidateId} · "
            + $"SHA {shortSha} · nicht aktiv";
    }
}
