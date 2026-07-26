using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Baut die Hauptcode-Abdeckung und schreibt Audit- sowie Inventardatei.</summary>
internal static class PersonalGoldInventoryWriter
{
    public static IReadOnlyList<PersonalGoldMainCodeStatus> BuildStatus(
        PersonalGoldFrameMigrationRequest request,
        IReadOnlyList<TrainingSample> selected,
        IReadOnlyDictionary<string, string> targetPaths)
    {
        var required = request.RequiredMainCodes
            .Select(NormalizeMainCode)
            .Where(code => code is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in selected.Select(sample => NormalizeMainCode(sample.Code)).Where(code => code is not null))
            required.Add(code!);

        return required
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(code =>
            {
                var codeSamples = selected
                    .Where(sample => string.Equals(
                        NormalizeMainCode(sample.Code),
                        code,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var full = codeSamples.Where(IsFullGold).ToArray();
                var fullFrames = full
                    .Select(sample => targetPaths[sample.SampleId])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var status = fullFrames == 0
                    ? "missing"
                    : fullFrames < request.TargetMinimumPerMainCode
                        ? "needs_more"
                        : fullFrames <= request.TargetMaximumPerMainCode
                            ? "ready"
                            : "above_target";
                return new PersonalGoldMainCodeStatus(
                    code,
                    codeSamples.Length,
                    codeSamples.Count(sample => sample.HasBbox),
                    full.Length,
                    fullFrames,
                    request.TargetMinimumPerMainCode,
                    request.TargetMaximumPerMainCode,
                    Math.Max(0, request.TargetMinimumPerMainCode - fullFrames),
                    status);
            })
            .ToArray();
    }

    public static async Task WriteAuditMappingAsync(
        string path,
        IReadOnlyList<TrainingSample> selected,
        IReadOnlyDictionary<string, string> targetPaths,
        IReadOnlyDictionary<string, string> oldDatabasePaths,
        PersonalGoldFrameMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var document = new
        {
            schema_version = 1,
            started_utc = request.StartedUtc,
            confirmed_by_user = request.ConfirmedByUser,
            samples = selected.Select(sample => new
            {
                sample_id = sample.SampleId,
                code = sample.Code,
                old_json_frame_path = sample.FramePath,
                old_database_frame_path = oldDatabasePaths.GetValueOrDefault(sample.SampleId),
                new_frame_path = targetPaths[sample.SampleId],
                has_bbox = ManualGoldTrainingPolicy.HasValidGoldBox(sample),
                has_segmentation = ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample)
            })
        };
        await AtomicTextFileWriter.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(document, JsonDefaults.Indented),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task WriteInventoryAsync(
        string path,
        PersonalGoldFrameMigrationRequest request,
        IReadOnlyList<TrainingSample> selected,
        IReadOnlyDictionary<string, string> targetPaths,
        IReadOnlyList<PersonalGoldMainCodeStatus> statuses,
        CancellationToken cancellationToken)
    {
        var document = new
        {
            schema_version = 1,
            generated_utc = DateTimeOffset.UtcNow,
            confirmed_by_user = request.ConfirmedByUser,
            selection_policy = "personal-manual-confirmed",
            full_gold_rule = "bbox-and-segmentation",
            target_per_main_code = new
            {
                minimum = request.TargetMinimumPerMainCode,
                maximum = request.TargetMaximumPerMainCode
            },
            total_personal_samples = selected.Count,
            total_full_gold_samples = selected.Count(IsFullGold),
            unique_gold_frames = targetPaths.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            main_codes = statuses,
            samples = selected.Select(sample => new
            {
                sample_id = sample.SampleId,
                code = sample.Code,
                main_code = NormalizeMainCode(sample.Code),
                frame_path = targetPaths[sample.SampleId],
                has_bbox = ManualGoldTrainingPolicy.HasValidGoldBox(sample),
                has_segmentation = ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample),
                full_gold = IsFullGold(sample)
            })
        };
        await AtomicTextFileWriter.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(document, JsonDefaults.Indented),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static bool IsFullGold(TrainingSample sample)
        => ManualGoldTrainingPolicy.HasValidGoldGeometry(sample);

    private static string? NormalizeMainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var normalized = code.Trim().Replace(".", string.Empty).ToUpperInvariant();
        return normalized.Length >= 3 ? normalized[..3] : null;
    }
}
