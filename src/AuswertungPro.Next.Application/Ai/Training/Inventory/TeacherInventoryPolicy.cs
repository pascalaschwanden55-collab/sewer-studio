using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Reine Fachregeln fuer Teacher-Datensaetze.</summary>
public static partial class TeacherInventoryPolicy
{
    private static readonly HashSet<string> GenericHoldings = new(
        ["Training", "Unbekannt", "Unknown"],
        StringComparer.OrdinalIgnoreCase);

    public static TrainingInventoryBoxState ClassifyBox(TeacherAnnotation annotation)
    {
        var box = annotation.BoundingBox;
        if (box is null
            || !double.IsFinite(box.Width)
            || !double.IsFinite(box.Height)
            || box.Width <= 0
            || box.Height <= 0)
        {
            return TrainingInventoryBoxState.MissingOrNonPositiveArea;
        }

        if (!double.IsFinite(box.XCenter) || !double.IsFinite(box.YCenter))
            return TrainingInventoryBoxState.NonFiniteCoordinates;

        var normalized = box.XCenter is >= 0 and <= 1
                         && box.YCenter is >= 0 and <= 1
                         && box.Width <= 1
                         && box.Height <= 1;
        if (!normalized)
            return TrainingInventoryBoxState.PositiveOutOfNormalizedRange;

        const double tolerance = 1e-9;
        var insideImage = box.XCenter - (box.Width / 2) >= -tolerance
                          && box.YCenter - (box.Height / 2) >= -tolerance
                          && box.XCenter + (box.Width / 2) <= 1 + tolerance
                          && box.YCenter + (box.Height / 2) <= 1 + tolerance;
        return insideImage
            ? TrainingInventoryBoxState.Valid
            : TrainingInventoryBoxState.ExtendsOutsideImage;
    }

    public static TeacherInventoryHoldingAssessment ClassifyHolding(TeacherAnnotation annotation)
    {
        var rawHolding = annotation.HaltungName?.Trim();
        if (!string.IsNullOrWhiteSpace(rawHolding)
            && !GenericHoldings.Contains(rawHolding)
            && HoldingPattern().IsMatch(rawHolding))
        {
            return new TeacherInventoryHoldingAssessment(
                TrainingInventoryHoldingState.Explicit,
                null,
                Array.Empty<string>());
        }

        var candidates = new[]
            {
                annotation.VideoPath,
                annotation.FullFramePath,
                annotation.CroppedRegionPath,
                annotation.YoloAnnotationPath
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => HoldingPattern().Matches(value!).Select(match => match.Value))
            .Select(EvalContaminationGuard.NormalizeHaltungKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.Length switch
        {
            1 => new TeacherInventoryHoldingAssessment(
                TrainingInventoryHoldingState.SuggestionNeedsManualReview,
                candidates[0],
                candidates),
            > 1 => new TeacherInventoryHoldingAssessment(
                TrainingInventoryHoldingState.Ambiguous,
                null,
                candidates),
            _ => new TeacherInventoryHoldingAssessment(
                TrainingInventoryHoldingState.Unknown,
                null,
                candidates)
        };
    }

    [GeneratedRegex(@"\d[\d.]*[-/]\d[\d.]*", RegexOptions.CultureInvariant)]
    private static partial Regex HoldingPattern();
}

public sealed record TeacherInventoryHoldingAssessment(
    TrainingInventoryHoldingState State,
    string? SuggestedHolding,
    IReadOnlyList<string> Candidates)
{
    public bool IsExplicit => State == TrainingInventoryHoldingState.Explicit;
}
