using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Application.UseCases.GoldQualityReview;

public sealed record GoldQualityReviewDataSnapshot(
    IReadOnlyList<TrainingSample> TrainingSamples,
    IReadOnlySet<string> ProtectedImageHashes,
    IReadOnlySet<string> ProtectedHoldingKeys,
    string ProtectionFingerprint);

public interface IGoldQualityReviewSnapshotProvider
{
    Task<GoldQualityReviewDataSnapshot> LoadAsync(
        IReadOnlyDictionary<string, string> protectedSetRootPaths,
        CancellationToken cancellationToken = default);
}

public interface IGoldQualityReviewSessionStore
{
    GoldQualityReviewSession? LoadCurrent(string reviewer);

    void SaveCurrent(GoldQualityReviewSession session);

    IReadOnlySet<string> LoadCompletedSampleIds(GoldQualityReviewSession session);

    void MarkCompleted(
        GoldQualityReviewSession session,
        string sampleId,
        DateTimeOffset completedUtc);
}

public sealed record GoldQualityReviewSessionEntry(
    string SampleId,
    string MainCode,
    DateTimeOffset BaselineConfirmedAtUtc,
    string ImageSha256);

public sealed record GoldQualityReviewSession(
    string SchemaVersion,
    string SessionId,
    DateTimeOffset CreatedUtc,
    string Reviewer,
    string RegistryHash,
    string ProtectionFingerprint,
    IReadOnlyList<string> MainCodes,
    int SamplesPerMainCode,
    IReadOnlyList<GoldQualityReviewSessionEntry> Entries)
{
    public const string CurrentSchemaVersion = "1.0";
}

public sealed record GoldQualityReviewQueueRequest(string ConfirmedByUser)
{
    public static IReadOnlyList<string> DefaultMainCodes { get; } =
        ["BAB", "BAF", "BAI", "BAJ", "BBC", "BBF"];

    public IReadOnlyList<string> MainCodes { get; init; } = DefaultMainCodes;

    public int SamplesPerMainCode { get; init; } = 15;
}

public sealed record GoldQualityReviewQueueResult(
    IReadOnlyList<WorkbenchItem> Items,
    int TotalCount,
    int CompletedCount,
    string SessionId,
    bool Resumed);

public sealed record GoldQualityReviewCompletionRequest(
    string SessionId,
    string SampleId,
    string ConfirmedByUser);

public interface IGoldQualityReviewQueueUseCase
{
    Task<GoldQualityReviewQueueResult> ExecuteAsync(
        GoldQualityReviewQueueRequest request,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        GoldQualityReviewCompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Baut oder setzt eine feste persoenliche Goldpruefung fort. Der Umfang stammt
/// ausschliesslich aus den explizit freigegebenen Train-/Validation-Sample-IDs des
/// Exportregisters. Schutzstand, Bildbytes und Ausgangsbestaetigung werden in einer
/// kleinen Sitzung gebunden; ein Holdout- oder Dateistandwechsel sperrt fail-closed.
/// </summary>
public sealed class GoldQualityReviewQueueUseCase : IGoldQualityReviewQueueUseCase
{
    private readonly IGoldQualityReviewSnapshotProvider _snapshotProvider;
    private readonly ITrainingExportRegistryStore _registryStore;
    private readonly IGoldQualityReviewSessionStore _sessionStore;
    private readonly Func<string, bool> _frameIsReadable;
    private readonly Func<string, (int Width, int Height)?> _readImageDimensions;
    private readonly Func<string, string?> _computeFileHash;
    private readonly Func<DateTimeOffset> _utcNow;

    public GoldQualityReviewQueueUseCase(
        IGoldQualityReviewSnapshotProvider snapshotProvider,
        ITrainingExportRegistryStore registryStore,
        IGoldQualityReviewSessionStore sessionStore,
        Func<string, bool> frameIsReadable,
        Func<string, (int Width, int Height)?> readImageDimensions,
        Func<string, string?> computeFileHash,
        Func<DateTimeOffset>? utcNow = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _registryStore = registryStore ?? throw new ArgumentNullException(nameof(registryStore));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _frameIsReadable = frameIsReadable ?? throw new ArgumentNullException(nameof(frameIsReadable));
        _readImageDimensions = readImageDimensions ?? throw new ArgumentNullException(nameof(readImageDimensions));
        _computeFileHash = computeFileHash ?? throw new ArgumentNullException(nameof(computeFileHash));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<GoldQualityReviewQueueResult> ExecuteAsync(
        GoldQualityReviewQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reviewer = request.ConfirmedByUser?.Trim() ?? string.Empty;
        if (reviewer.Length == 0)
            throw new ArgumentException("Bearbeiter fuer die Goldpruefung fehlt.", nameof(request));
        if (request.SamplesPerMainCode <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Die Anzahl je Hauptcode muss positiv sein.");

        var mainCodes = NormalizeMainCodes(request.MainCodes);
        var registryBundle = _registryStore.ReadBundle();
        var registry = registryBundle.Snapshot;
        ValidateRegistry(registry);
        var snapshot = await _snapshotProvider
            .LoadAsync(registryBundle.ProtectedSetRootPaths, cancellationToken)
            .ConfigureAwait(false);
        ValidateProtection(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var samplesById = BuildUniqueSampleIndex(snapshot.TrainingSamples, registry.ApprovedSampleIds);
        var currentSession = _sessionStore.LoadCurrent(reviewer);
        if (currentSession is not null)
        {
            ValidateSession(currentSession, reviewer, mainCodes, request.SamplesPerMainCode, registry, snapshot);
            return ResumeSession(currentSession, samplesById, registry, snapshot, reviewer, cancellationToken);
        }

        var candidates = BuildCandidates(
            samplesById.Values,
            registry,
            snapshot,
            reviewer,
            mainCodes,
            requireCompleteGold: true,
            cancellationToken);
        var selected = SelectBalanced(candidates, mainCodes, request.SamplesPerMainCode);
        var createdUtc = _utcNow().ToUniversalTime();
        var entries = selected
            .Select(candidate => new GoldQualityReviewSessionEntry(
                candidate.Sample.SampleId,
                candidate.MainCode,
                ToUtc(candidate.Sample.ConfirmedAtUtc!.Value),
                candidate.ImageSha256))
            .ToArray();
        var session = new GoldQualityReviewSession(
            GoldQualityReviewSession.CurrentSchemaVersion,
            BuildSessionId(reviewer, registry.RegistryHash, snapshot.ProtectionFingerprint, createdUtc, entries),
            createdUtc,
            reviewer,
            registry.RegistryHash,
            snapshot.ProtectionFingerprint,
            mainCodes,
            request.SamplesPerMainCode,
            entries);
        _sessionStore.SaveCurrent(session);

        return new GoldQualityReviewQueueResult(
            selected.Select(candidate => ToWorkbenchItem(
                candidate.Sample,
                candidate.Dimensions,
                candidate.ImageSha256,
                ToUtc(candidate.Sample.ConfirmedAtUtc!.Value))).ToArray(),
            entries.Length,
            CompletedCount: 0,
            session.SessionId,
            Resumed: false);
    }

    public Task MarkCompletedAsync(
        GoldQualityReviewCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var reviewer = request.ConfirmedByUser?.Trim() ?? string.Empty;
        var sampleId = request.SampleId?.Trim() ?? string.Empty;
        var sessionId = request.SessionId?.Trim() ?? string.Empty;
        if (reviewer.Length == 0 || sampleId.Length == 0 || sessionId.Length == 0)
            throw new ArgumentException("Sitzung, Sample und Bearbeiter muessen angegeben sein.", nameof(request));

        var session = _sessionStore.LoadCurrent(reviewer)
                      ?? throw new InvalidOperationException(
                          "Goldpruefung kann nicht abgeschlossen werden: Die Sitzung fehlt.");
        if (!string.Equals(session.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
            || !session.Entries.Any(entry => string.Equals(
                entry.SampleId,
                sampleId,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Goldpruefung kann nicht abgeschlossen werden: Sitzung oder Sample passt nicht.");
        }

        _sessionStore.MarkCompleted(session, sampleId, _utcNow().ToUniversalTime());
        return Task.CompletedTask;
    }

    private GoldQualityReviewQueueResult ResumeSession(
        GoldQualityReviewSession session,
        IReadOnlyDictionary<string, TrainingSample> samplesById,
        TrainingExportRegistrySnapshot registry,
        GoldQualityReviewDataSnapshot snapshot,
        string reviewer,
        CancellationToken cancellationToken)
    {
        var open = new List<WorkbenchItem>();
        var completed = 0;
        var completedSampleIds = _sessionStore.LoadCompletedSampleIds(session);
        foreach (var entry in session.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!samplesById.TryGetValue(entry.SampleId, out var sample))
            {
                throw new InvalidOperationException(
                    $"Goldpruefung kann nicht fortgesetzt werden: Sample '{entry.SampleId}' fehlt oder ist nicht mehr freigegeben.");
            }

            var candidate = BuildCandidate(
                sample,
                registry,
                snapshot,
                reviewer,
                requireCompleteGold: false);
            if (candidate is null)
            {
                throw new InvalidOperationException(
                    $"Goldpruefung kann nicht fortgesetzt werden: Sample '{entry.SampleId}' ist nicht mehr sicher lesbar oder geschuetzt.");
            }
            if (!string.Equals(candidate.ImageSha256, entry.ImageSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Goldpruefung kann nicht fortgesetzt werden: Bildbytes von Sample '{entry.SampleId}' haben sich geaendert.");
            }

            if (completedSampleIds.Contains(entry.SampleId)
                && IsCompleteGoldForFrame(sample, reviewer, candidate.Dimensions))
            {
                completed++;
                continue;
            }

            open.Add(ToWorkbenchItem(
                sample,
                candidate.Dimensions,
                entry.ImageSha256,
                sample.ConfirmedAtUtc.HasValue
                    ? ToUtc(sample.ConfirmedAtUtc.Value)
                    : null));
        }

        return new GoldQualityReviewQueueResult(
            open,
            session.Entries.Count,
            completed,
            session.SessionId,
            Resumed: true);
    }

    private IReadOnlyList<Candidate> BuildCandidates(
        IEnumerable<TrainingSample> samples,
        TrainingExportRegistrySnapshot registry,
        GoldQualityReviewDataSnapshot snapshot,
        string reviewer,
        IReadOnlyList<string> mainCodes,
        bool requireCompleteGold,
        CancellationToken cancellationToken)
    {
        var allowedCodes = mainCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<Candidate>();
        foreach (var sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mainCode = NormalizeMainCode(sample.Code);
            if (mainCode is null || !allowedCodes.Contains(mainCode))
                continue;

            var candidate = BuildCandidate(sample, registry, snapshot, reviewer, requireCompleteGold);
            if (candidate is not null)
                result.Add(candidate);
        }

        return result;
    }

    private Candidate? BuildCandidate(
        TrainingSample sample,
        TrainingExportRegistrySnapshot registry,
        GoldQualityReviewDataSnapshot snapshot,
        string reviewer,
        bool requireCompleteGold)
    {
        if (string.IsNullOrWhiteSpace(sample.SampleId)
            || !registry.ApprovedSampleIds.Contains(sample.SampleId)
            || !TryGetRegistryHoldingRole(registry, sample.CaseId, out _)
            || EvalContaminationGuard.IsEvalHaltung(snapshot.ProtectedHoldingKeys, sample.CaseId)
            || string.IsNullOrWhiteSpace(sample.FramePath))
        {
            return null;
        }

        var mainCode = NormalizeMainCode(sample.Code);
        if (mainCode is null)
            return null;
        if (requireCompleteGold
            && !ManualGoldTrainingPolicy.EvaluateForExport(sample, reviewer).IsEligible)
        {
            return null;
        }

        if (!SafeFrameReadable(sample.FramePath))
            return null;
        var dimensions = SafeReadDimensions(sample.FramePath);
        if (dimensions is null)
            return null;
        var imageSha256 = SafeComputeHash(sample.FramePath);
        if (!IsSha256(imageSha256)
            || snapshot.ProtectedImageHashes.Contains(imageSha256!))
        {
            return null;
        }
        if (requireCompleteGold
            && (sample.SamMaskImageWidth != dimensions.Value.Width
                || sample.SamMaskImageHeight != dimensions.Value.Height))
            return null;

        return new Candidate(
            sample,
            mainCode,
            imageSha256!,
            PhysicalHoldingKey(sample.CaseId),
            dimensions.Value);
    }

    private static IReadOnlyList<Candidate> SelectBalanced(
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<string> mainCodes,
        int samplesPerMainCode)
    {
        var selected = new List<Candidate>(mainCodes.Count * samplesPerMainCode);
        var usedImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedHoldings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mainCode in mainCodes)
        {
            var ordered = candidates
                .Where(candidate => string.Equals(candidate.MainCode, mainCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.Sample.ConfirmedAtUtc ?? DateTime.MaxValue)
                .ThenBy(candidate => candidate.PhysicalHoldingKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Sample.SampleId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var codeSelection = new List<Candidate>(samplesPerMainCode);

            AddCandidates(
                ordered.Where(candidate => !usedHoldings.Contains(candidate.PhysicalHoldingKey)),
                codeSelection,
                usedImages,
                usedHoldings,
                samplesPerMainCode);
            AddCandidates(ordered, codeSelection, usedImages, usedHoldings, samplesPerMainCode);
            if (codeSelection.Count != samplesPerMainCode)
            {
                throw new InvalidOperationException(
                    $"Goldpruefung wurde nicht angelegt: Fuer {mainCode} sind nur {codeSelection.Count} " +
                    $"sichere unterschiedliche Bilder verfuegbar, benoetigt werden {samplesPerMainCode}.");
            }

            selected.AddRange(codeSelection);
        }

        return selected;
    }

    private static void AddCandidates(
        IEnumerable<Candidate> candidates,
        ICollection<Candidate> selected,
        ISet<string> usedImages,
        ISet<string> usedHoldings,
        int targetCount)
    {
        foreach (var candidate in candidates)
        {
            if (selected.Count >= targetCount)
                return;
            if (!usedImages.Add(candidate.ImageSha256))
                continue;

            selected.Add(candidate);
            usedHoldings.Add(candidate.PhysicalHoldingKey);
        }
    }

    private static WorkbenchItem ToWorkbenchItem(
        TrainingSample sample,
        (int Width, int Height) dimensions,
        string expectedImageSha256,
        DateTimeOffset? expectedConfirmedAtUtc)
    {
        var sourceSuggestion = BuildSourceSuggestion(sample);
        var existingSegmentation = BuildExistingSegmentation(sample, dimensions);
        return new WorkbenchItem(
            sample.FramePath,
            sample.CaseId,
            sample.MeterStart,
            sample.MeterEnd,
            HaltungName: string.IsNullOrWhiteSpace(sample.CaseId) ? null : sample.CaseId,
            VideoPath: null,
            PipeDiameterMm: null,
            ExistingSampleId: sample.SampleId,
            ExistingCode: sample.Code,
            ExistingBeschreibung: sample.Beschreibung,
            IsStreckenschaden: sample.IsStreckenschaden,
            SourceSuggestion: sourceSuggestion)
        {
            InspectionDate = sample.InspectionDate,
            ExistingSourceType = sample.SourceType,
            ExistingNotes = sample.Notes,
            ExistingBox = ManualGoldTrainingPolicy.HasValidGoldBox(sample)
                ? new BoundingBox(
                    sample.BboxXCenter!.Value,
                    sample.BboxYCenter!.Value,
                    sample.BboxWidth!.Value,
                    sample.BboxHeight!.Value)
                : null,
            ExistingSegmentation = existingSegmentation,
            ExistingClockPosition = ReadExistingClockPosition(sample),
            ExistingSeverity = ReadExistingSeverity(sample),
            ExpectedImageSha256 = expectedImageSha256,
            ExpectedConfirmedAtUtc = expectedConfirmedAtUtc,
        };
    }

    private static double? ReadExistingClockPosition(TrainingSample sample)
    {
        var parameters = sample.CodeMeta?.Parameters;
        if (parameters is null)
            return null;

        foreach (var key in new[] { "vsa.uhr.von", "ClockPos1", "Uhr_von" })
        {
            if (!parameters.TryGetValue(key, out var raw))
                continue;

            var numericPart = raw.Trim();
            var colonIndex = numericPart.IndexOf(':');
            if (colonIndex >= 0)
                numericPart = numericPart[..colonIndex];
            if (double.TryParse(
                    numericPart,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value)
                && value is >= 0 and <= 12)
            {
                return value;
            }
        }

        return null;
    }

    private static int? ReadExistingSeverity(TrainingSample sample)
        => int.TryParse(
            sample.CodeMeta?.Severity,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var severity)
           && severity is >= 1 and <= 5
            ? severity
            : null;

    private static WorkbenchSegmentation? BuildExistingSegmentation(
        TrainingSample sample,
        (int Width, int Height) dimensions)
    {
        if (!ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample)
            || sample.SamMaskImageWidth != dimensions.Width
            || sample.SamMaskImageHeight != dimensions.Height)
        {
            return null;
        }

        var areaPixels = sample.SamMaskAreaPixels;
        double? areaPercent = areaPixels.HasValue
            ? 100.0 * areaPixels.Value / (dimensions.Width * (double)dimensions.Height)
            : null;
        return new WorkbenchSegmentation(
            sample.SamMaskRle,
            dimensions.Width,
            dimensions.Height,
            areaPercent,
            "Gespeicherte Goldmaske",
            Degraded: false,
            areaPixels,
            sample.SamMaskConfidence,
            sample.SamMaskLabel);
    }

    private static WorkbenchSourceSuggestion? BuildSourceSuggestion(TrainingSample sample)
    {
        if (!string.Equals(sample.SourceType, SourceTypeNames.PdfPhoto, StringComparison.OrdinalIgnoreCase)
            || !PdfGoldProvenancePolicy.TryParse(sample.Notes, out var provenance)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceDescription))
        {
            return null;
        }

        return new WorkbenchSourceSuggestion(
            sample.SourceReferenceCode,
            sample.SourceReferenceDescription,
            provenance.SourceDocumentName,
            provenance.SourceDocumentSha256,
            provenance.PageNumber,
            provenance.PhotoId,
            provenance.MatchKind)
        {
            InspectionDate = sample.InspectionDate,
        };
    }

    private static bool IsCompleteGoldForFrame(
        TrainingSample sample,
        string reviewer,
        (int Width, int Height) dimensions)
        => ManualGoldTrainingPolicy.EvaluateForExport(sample, reviewer).IsEligible
           && sample.SamMaskImageWidth == dimensions.Width
           && sample.SamMaskImageHeight == dimensions.Height;

    private static IReadOnlyDictionary<string, TrainingSample> BuildUniqueSampleIndex(
        IReadOnlyList<TrainingSample> samples,
        IReadOnlySet<string> approvedSampleIds)
    {
        var result = new Dictionary<string, TrainingSample>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples.Where(sample => approvedSampleIds.Contains(sample.SampleId)))
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId))
                continue;
            if (!result.TryAdd(sample.SampleId, sample))
            {
                throw new InvalidOperationException(
                    $"Goldpruefung gesperrt: Sample-ID '{sample.SampleId}' ist im Bestand nicht eindeutig.");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeMainCodes(IReadOnlyList<string>? mainCodes)
    {
        if (mainCodes is null || mainCodes.Count == 0)
            throw new ArgumentException("Mindestens ein Hauptcode ist erforderlich.", nameof(mainCodes));

        var normalized = mainCodes
            .Select(NormalizeMainCode)
            .ToArray();
        if (normalized.Any(code => code is null)
            || normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            throw new ArgumentException("Hauptcodes muessen eindeutig und genau dreistellig sein.", nameof(mainCodes));
        }

        return normalized.Cast<string>().ToArray();
    }

    private static string? NormalizeMainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var normalized = code.Trim().Replace(".", string.Empty).ToUpperInvariant();
        return normalized.Length >= 3 ? normalized[..3] : null;
    }

    private static bool TryGetRegistryHoldingRole(
        TrainingExportRegistrySnapshot registry,
        string? caseId,
        out TrainingExportHoldingRole role)
    {
        var key = EvalContaminationGuard.NormalizeHaltungKey(caseId);
        if (!string.IsNullOrWhiteSpace(key)
            && registry.HoldingRoles.TryGetValue(key, out role))
        {
            return true;
        }

        var parts = key?.Split('-', StringSplitOptions.None);
        if (parts is { Length: 2 }
            && registry.HoldingRoles.TryGetValue($"{parts[1]}-{parts[0]}", out role))
        {
            return true;
        }

        role = default;
        return false;
    }

    private static string PhysicalHoldingKey(string caseId)
    {
        var key = EvalContaminationGuard.NormalizeHaltungKey(caseId) ?? caseId.Trim();
        var parts = key.Split('-', StringSplitOptions.None);
        if (parts.Length != 2)
            return key;
        return StringComparer.OrdinalIgnoreCase.Compare(parts[0], parts[1]) <= 0
            ? $"{parts[0]}|{parts[1]}"
            : $"{parts[1]}|{parts[0]}";
    }

    private bool SafeFrameReadable(string path)
    {
        try { return _frameIsReadable(path); }
        catch { return false; }
    }

    private (int Width, int Height)? SafeReadDimensions(string path)
    {
        try { return _readImageDimensions(path); }
        catch { return null; }
    }

    private string? SafeComputeHash(string path)
    {
        try { return _computeFileHash(path)?.Trim().ToLowerInvariant(); }
        catch { return null; }
    }

    private static void ValidateRegistry(TrainingExportRegistrySnapshot registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (registry.ApprovalStatus != TrainingExportRegistryApprovalStatus.Approved
            || string.IsNullOrWhiteSpace(registry.ApprovedBy)
            || registry.ApprovedSampleIds.Count == 0
            || !IsSha256(registry.RegistryHash))
        {
            throw new InvalidOperationException(
                "Goldpruefung gesperrt: Das Exportregister ist nicht vollstaendig persoenlich freigegeben.");
        }
    }

    private static void ValidateProtection(GoldQualityReviewDataSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!IsSha256(snapshot.ProtectionFingerprint))
        {
            throw new InvalidOperationException(
                "Goldpruefung gesperrt: Der aktuelle Eval-Schutzstand ist nicht vollstaendig gebunden.");
        }
    }

    private static void ValidateSession(
        GoldQualityReviewSession session,
        string reviewer,
        IReadOnlyList<string> mainCodes,
        int samplesPerMainCode,
        TrainingExportRegistrySnapshot registry,
        GoldQualityReviewDataSnapshot snapshot)
    {
        if (!string.Equals(session.SchemaVersion, GoldQualityReviewSession.CurrentSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(session.Reviewer, reviewer, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(session.RegistryHash, registry.RegistryHash, StringComparison.OrdinalIgnoreCase)
            || session.SamplesPerMainCode != samplesPerMainCode
            || !session.MainCodes.SequenceEqual(mainCodes, StringComparer.OrdinalIgnoreCase)
            || session.MainCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != session.MainCodes.Count
            || session.Entries.Count != mainCodes.Count * samplesPerMainCode
            || session.Entries.Select(entry => entry.SampleId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
               != session.Entries.Count
            || session.Entries.Any(entry => !IsSha256(entry.ImageSha256))
            || mainCodes.Any(mainCode =>
                session.Entries.Count(entry => string.Equals(
                    entry.MainCode,
                    mainCode,
                    StringComparison.OrdinalIgnoreCase)) != samplesPerMainCode)
            || !string.Equals(
                session.SessionId,
                BuildSessionId(
                    session.Reviewer,
                    session.RegistryHash,
                    session.ProtectionFingerprint,
                    session.CreatedUtc,
                    session.Entries),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Goldpruefung kann nicht fortgesetzt werden: Das Sitzungsmanifest passt nicht mehr zum freigegebenen Umfang.");
        }
        if (!string.Equals(
                session.ProtectionFingerprint,
                snapshot.ProtectionFingerprint,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Goldpruefung kann nicht fortgesetzt werden: Der Eval-Schutzstand hat sich geaendert.");
        }
    }

    private static DateTimeOffset ToUtc(DateTime value)
        => new(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(), TimeSpan.Zero);

    private static bool IsSha256(string? value)
        => value is { Length: 64 }
           && value.All(character => character is >= '0' and <= '9'
                                     or >= 'a' and <= 'f'
                                     or >= 'A' and <= 'F');

    private static string BuildSessionId(
        string reviewer,
        string registryHash,
        string protectionFingerprint,
        DateTimeOffset createdUtc,
        IReadOnlyList<GoldQualityReviewSessionEntry> entries)
    {
        var text = string.Join(
            '\n',
            reviewer,
            registryHash,
            protectionFingerprint,
            createdUtc.ToString("O"),
            string.Join('\n', entries.Select(entry =>
                $"{entry.SampleId}|{entry.MainCode}|{entry.BaselineConfirmedAtUtc:O}|{entry.ImageSha256}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private sealed record Candidate(
        TrainingSample Sample,
        string MainCode,
        string ImageSha256,
        string PhysicalHoldingKey,
        (int Width, int Height) Dimensions);
}
