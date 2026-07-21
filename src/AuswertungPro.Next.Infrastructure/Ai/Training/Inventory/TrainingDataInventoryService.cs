using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

/// <summary>
/// Orchestriert das rein lesende Inventar. Bericht und Export-Snapshot werden aus
/// demselben Scan-Kern gebaut, damit es keine zweite Datenwahrheit gibt.
/// </summary>
public sealed class TrainingDataInventoryService : ITrainingDataInventoryService
{
    private const int ProgressReportInterval = 25;
    private readonly TimeProvider _timeProvider;

    public TrainingDataInventoryService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TrainingDataInventoryReport> InspectAsync(
        TrainingDataInventoryRequest request,
        IProgress<TrainingDataInventoryProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => (await InspectCoreAsync(request, progress, cancellationToken).ConfigureAwait(false)).Report;

    public Task<TrainingDataInventoryRuntimeSnapshot> InspectRuntimeSnapshotAsync(
        TrainingDataInventoryRequest request,
        IProgress<TrainingDataInventoryProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => InspectCoreAsync(request, progress, cancellationToken);

    private async Task<TrainingDataInventoryRuntimeSnapshot> InspectCoreAsync(
        TrainingDataInventoryRequest request,
        IProgress<TrainingDataInventoryProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knowledgeRoot = TrainingInventoryPaths.NormalizeRequired(request.KnowledgeRoot);
        var evalSetRoot = TrainingInventoryPaths.NormalizeOptional(request.EvalSetRoot);
        var searchRoots = TrainingInventoryPaths.NormalizeDistinct(request.SearchRoots);
        var protectedRoots = TrainingInventoryPaths.NormalizeDistinct(
            request.ProtectedRoots.Concat(
                    string.IsNullOrWhiteSpace(evalSetRoot) ? [] : [evalSetRoot])
                .Concat(request.ProtectedSetRoots.Values));
        var issues = new List<TrainingInventoryIssue>();
        var skippedDirectories = new List<string>();

        TrainingInventoryIssueCollector.AddMissingRoots(searchRoots, protectedRoots, issues);

        progress?.Report(new TrainingDataInventoryProgress("Dateien suchen", 0));
        var pathResolver = TrainingInventoryPathResolver.Create(
            knowledgeRoot,
            searchRoots,
            protectedRoots,
            request.ComputeAssetHashes,
            skippedDirectories,
            cancellationToken);
        TrainingInventoryIssueCollector.AddSkippedDirectories(skippedDirectories, issues);

        var sourceSpecs = TrainingInventorySourceReader.Discover(
            knowledgeRoot,
            request.IncludeBackups,
            issues);
        var sourceDocuments = new List<TrainingInventorySourceDocument>(sourceSpecs.Count);
        IReadOnlyList<TeacherAnnotation> teacherAnnotations = Array.Empty<TeacherAnnotation>();
        IReadOnlyList<TrainingSample> trainingSamples = Array.Empty<TrainingSample>();

        for (var index = 0; index < sourceSpecs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new TrainingDataInventoryProgress("Quellen lesen", index, sourceSpecs.Count));

            var result = await TrainingInventorySourceReader
                .ReadAsync(sourceSpecs[index], cancellationToken)
                .ConfigureAwait(false);
            sourceDocuments.Add(result.Document);
            TrainingInventoryIssueCollector.AddSource(result.Document, issues);

            if (sourceSpecs[index].Role != TrainingInventorySourceRole.Current)
                continue;
            if (sourceSpecs[index].DataKind == TrainingInventoryDataKind.TeacherAnnotations
                && result.TeacherAnnotations is not null)
            {
                teacherAnnotations = result.TeacherAnnotations;
            }
            else if (sourceSpecs[index].DataKind == TrainingInventoryDataKind.TrainingSamples
                     && result.TrainingSamples is not null)
            {
                trainingSamples = result.TrainingSamples;
            }
        }

        progress?.Report(new TrainingDataInventoryProgress(
            "Eval-Schutz laden",
            sourceSpecs.Count,
            sourceSpecs.Count));
        var evalProtection = await ReadProtectionAsync(
                request,
                evalSetRoot,
                cancellationToken)
            .ConfigureAwait(false);
        TrainingInventoryIssueCollector.AddEvalProtection(
            evalSetRoot,
            evalProtection.Status,
            issues);
        var teacherInspector = new TeacherInventoryInspector(
            pathResolver: pathResolver,
            evalChecksEnabled: request.ComputeAssetHashes,
            evalProtectionAvailable: evalProtection.Status.Complete,
            evalImageHashes: evalProtection.ImageHashes,
            evalHoldingKeys: evalProtection.HoldingKeys);
        var teacherRecords = new List<TeacherInventoryRecord>(teacherAnnotations.Count);

        for (var index = 0; index < teacherAnnotations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index % ProgressReportInterval == 0)
            {
                progress?.Report(new TrainingDataInventoryProgress(
                    "Teacher-Daten pruefen",
                    index,
                    teacherAnnotations.Count));
            }

            var annotation = teacherAnnotations[index];
            var record = await teacherInspector
                .InspectAsync(annotation, index, cancellationToken)
                .ConfigureAwait(false);
            teacherRecords.Add(record);
            TrainingInventoryIssueCollector.AddRecord(record, issues);
        }

        teacherRecords.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RecordKey, right.RecordKey));
        progress?.Report(new TrainingDataInventoryProgress(
            "Bericht fertigstellen",
            teacherAnnotations.Count,
            teacherAnnotations.Count));

        var report = new TrainingDataInventoryReport
        {
            GeneratedUtc = _timeProvider.GetUtcNow(),
            EvalProtection = evalProtection.Status,
            KnowledgeRoot = knowledgeRoot,
            EvalSetRoot = evalSetRoot,
            SearchRoots = searchRoots,
            ProtectedRoots = protectedRoots,
            Sources = sourceDocuments,
            TeacherRecords = teacherRecords,
            Issues = issues,
            SkippedDirectories = skippedDirectories
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Summary = TrainingInventorySummaryBuilder.Build(teacherRecords, sourceDocuments)
        };
        var protectedSets = evalProtection.ProtectedSets
            .OrderBy(set => set.SetId, StringComparer.OrdinalIgnoreCase)
            .Select(set => new TrainingInventoryProtectedSetSnapshot(
                set.SetId,
                set.RootPath,
                set.ManifestSha256))
            .ToArray();
        var protection = new TrainingInventoryProtectionSnapshot(
            evalProtection.Status,
            evalProtection.ImageHashes,
            evalProtection.HoldingKeys,
            protectedSets,
            ComputeProtectionFingerprint(
                evalProtection.Status,
                evalProtection.ImageHashes,
                evalProtection.HoldingKeys,
                protectedSets));

        return new TrainingDataInventoryRuntimeSnapshot(
            report,
            teacherAnnotations,
            trainingSamples,
            protection);
    }

    private static async Task<TrainingInventoryEvalProtectionReadResult> ReadProtectionAsync(
        TrainingDataInventoryRequest request,
        string? evalSetRoot,
        CancellationToken cancellationToken)
    {
        if (request.ProtectedSetRoots.Count == 0)
        {
            return await TrainingInventoryEvalProtectionReader
                .ReadAsync(evalSetRoot, request.ComputeAssetHashes, cancellationToken)
                .ConfigureAwait(false);
        }

        var statuses = new List<TrainingInventoryEvalSetStatus>();
        var discoveryErrors = new List<string>();
        var imageHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var holdingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var protectedSets = new List<TrainingInventoryProtectedSetRead>();
        foreach (var expected in request.ProtectedSetRoots
                     .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(expected.Key) || string.IsNullOrWhiteSpace(expected.Value))
            {
                discoveryErrors.Add("Exportregister enthaelt eine leere Schutz-Set-ID oder einen leeren Pfad.");
                continue;
            }

            var expectedRoot = Path.GetFullPath(expected.Value);
            var result = await TrainingInventoryEvalProtectionReader
                .ReadAsync(expectedRoot, request.ComputeAssetHashes, cancellationToken)
                .ConfigureAwait(false);
            statuses.AddRange(result.Status.Sets);
            discoveryErrors.AddRange(result.Status.DiscoveryErrors.Select(error => $"{expected.Key}: {error}"));
            imageHashes.UnionWith(result.ImageHashes);
            holdingKeys.UnionWith(result.HoldingKeys);
            var exactSets = result.ProtectedSets
                .Where(set => Path.GetFullPath(set.RootPath).Equals(
                    expectedRoot,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (exactSets.Length != 1 || result.ProtectedSets.Count != 1)
            {
                discoveryErrors.Add(
                    $"Schutz-Set '{expected.Key}' muss genau ein Manifest direkt im registrierten Ordner enthalten.");
                continue;
            }
            protectedSets.Add(new TrainingInventoryProtectedSetRead(
                expected.Key.Trim(),
                expectedRoot,
                exactSets[0].ManifestSha256));
        }

        return new TrainingInventoryEvalProtectionReadResult(
            new TrainingInventoryEvalProtectionStatus
            {
                ImageHashCheckEnabled = request.ComputeAssetHashes,
                Sets = statuses,
                DiscoveryErrors = discoveryErrors
            },
            imageHashes,
            holdingKeys,
            protectedSets);
    }

    private static string ComputeProtectionFingerprint(
        TrainingInventoryEvalProtectionStatus status,
        IReadOnlySet<string> imageHashes,
        IReadOnlySet<string> holdingKeys,
        IReadOnlyList<TrainingInventoryProtectedSetSnapshot> sets)
    {
        var builder = new StringBuilder()
            .Append(status.Complete).Append('|')
            .Append(status.ImageHashCheckEnabled).Append('\n');
        foreach (var set in sets.OrderBy(item => item.SetId, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("set|").Append(set.SetId).Append('|')
                .Append(set.ManifestSha256.ToLowerInvariant()).Append('\n');
        }
        foreach (var hash in imageHashes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            builder.Append("image|").Append(hash.ToLowerInvariant()).Append('\n');
        foreach (var holding in holdingKeys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            builder.Append("holding|").Append(holding).Append('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
