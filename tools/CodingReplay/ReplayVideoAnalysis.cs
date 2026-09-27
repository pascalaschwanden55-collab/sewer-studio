using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using System.Text;

namespace CodingReplay;

public sealed record ReplayVideoAnalysisContext(
    string Holding,
    string VideoPath,
    int DiameterMm,
    double ReachLengthM,
    double DurationSeconds,
    string? EvidencePhotoRoot = null);

public sealed record ReplayVideoEventEvidence(
    double? YoloConfidence,
    double? DinoConfidence,
    double? SamConfidence,
    string? SamMaskRleSha256,
    int? SamMaskImageWidth,
    int? SamMaskImageHeight,
    string? DetectorSource,
    string? DetectorArtifactSha256,
    string? DetectorPurpose,
    string? DetectorQualificationStatus,
    string? SuggestedByModelId,
    string? SuggestedByModelSha256,
    string? Reason);

public sealed record ReplayVideoPreviousEvidenceSnapshot(
    int Revision,
    string? FrameId,
    string? ImageSha256,
    Guid EntryId,
    string Code,
    double? MeterStart,
    double? MeterEnd,
    double MeterAtCapture,
    double VideoTimestampSeconds,
    IReadOnlyList<string> PhotoPaths,
    IReadOnlyList<string> OriginalPhotoPaths,
    DateTimeOffset? HumanTouchedAtUtc,
    double? YoloConfidence,
    double? DinoConfidence,
    double? SamConfidence,
    string? SamMaskRleSha256,
    int? SamMaskImageWidth,
    int? SamMaskImageHeight,
    string? SuggestedByModelId,
    string? SuggestedByModelSha256,
    string? DetectorSource,
    string? DetectorArtifactSha256,
    string? DetectorPurpose);

public sealed record ReplayVideoEventSnapshot(
    Guid EventId,
    Guid EntryId,
    string Code,
    double? MeterStart,
    double? MeterEnd,
    double MeterAtCapture,
    double VideoTimestampSeconds,
    string Source,
    string ReviewState,
    string? QualityGate,
    string MeterSource,
    string Origin,
    string? FrameId,
    string? ImageSha256,
    ReplayVideoEventEvidence Evidence,
    IReadOnlyList<string> PhotoPaths,
    IReadOnlyList<string> OriginalPhotoPaths,
    DateTimeOffset? HumanTouchedAtUtc,
    IReadOnlyList<ReplayVideoPreviousEvidenceSnapshot> PreviousEvidence);

public enum ReplayVideoEventChangeKind
{
    Added,
    Updated,
    Closed,
    Removed
}

public sealed record ReplayVideoEventChange(
    ReplayVideoEventChangeKind Kind,
    ReplayVideoEventSnapshot Event);

public sealed record ReplayVideoFrameObservation(
    string FrameId,
    double TimestampSeconds,
    string Outcome,
    double Meter,
    string MeterSource,
    IReadOnlyList<ReplayVideoEventChange> EventChanges,
    IReadOnlyList<ReplayVideoEventSnapshot> Events,
    IReadOnlyDictionary<string, string> Trace,
    string? TechnicalError = null);

public sealed record ReplayVideoFinalization(
    bool CanExit,
    IReadOnlyList<ReplayVideoEventChange> EventChanges,
    IReadOnlyList<ReplayVideoEventSnapshot> Events,
    int OpenStretchCount,
    bool TerminalBoundaryPresent);

/// <summary>
/// Eine einzige echte Codier-Sitzung fuer die festgelegte Bildfolge eines Videos.
/// Der Einzelbildweg bleibt getrennt und beginnt weiterhin ohne Verlauf.
/// </summary>
public sealed class ReplayVideoAnalysisSession
{
    private readonly ReplayCodingAnalyzer _analyzer;
    private readonly ReplayCodingState _state;
    private bool _finalized;

    internal ReplayVideoAnalysisSession(
        ReplayCodingAnalyzer analyzer,
        ReplayVideoAnalysisContext context,
        ReplayCandidateDetectionSet? candidateDetections = null)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.Holding)
            || string.IsNullOrWhiteSpace(context.VideoPath)
            || context.DiameterMm <= 0
            || !double.IsFinite(context.ReachLengthM) || context.ReachLengthM <= 0
            || !double.IsFinite(context.DurationSeconds) || context.DurationSeconds <= 0)
        {
            throw new ArgumentException("Videokontext ist ungueltig.", nameof(context));
        }

        _analyzer = analyzer;
        _state = ReplayCodingState.CreateVideo(context, candidateDetections);
    }

    public Guid SessionId => _state.Session.ActiveSession?.SessionId
        ?? throw new InvalidOperationException("Keine Replay-Sitzung aktiv.");

    public IReadOnlyList<ReplayVideoEventSnapshot> Events => _state.SnapshotEvents();

    public Task<ReplayVideoFrameObservation> AnalyzeFrameAsync(
        ReplayVideoFrame frame,
        byte[] image,
        CancellationToken ct = default)
    {
        if (_finalized)
            throw new InvalidOperationException("Der Videonachlauf ist bereits abgeschlossen.");
        return _analyzer.AnalyzeVideoFrameAsync(_state, frame, image, ct);
    }

    public ReplayVideoFinalization FinalizeSequence()
    {
        if (_finalized)
            throw new InvalidOperationException("Der Videonachlauf ist bereits abgeschlossen.");
        _finalized = true;
        return _analyzer.FinalizeVideo(_state);
    }
}

internal sealed class ReplayCodingState
{
    private readonly Dictionary<Guid, (string MeterSource, string Origin, string? FrameId, string? ImageSha256)> _eventOrigins = [];
    private readonly Dictionary<string, string> _frameEvidencePaths = new(StringComparer.Ordinal);

    private ReplayCodingState(
        CodingSessionService session,
        CodingStreckenschadenTrackerOwner tracker,
        ReplayVideoAnalysisContext? video,
        bool useFrameReadiness,
        ReplayCandidateDetectionSet? candidateDetections)
    {
        Session = session;
        Tracker = tracker;
        Video = video;
        UseFrameReadiness = useFrameReadiness;
        CandidateDetections = candidateDetections;
    }

    internal CodingSessionService Session { get; }
    internal CodingStreckenschadenTrackerOwner Tracker { get; }
    internal ReplayVideoAnalysisContext? Video { get; }
    internal CodingFrameReadinessTracker Readiness { get; } = new();
    internal bool UseFrameReadiness { get; }
    internal ReplayCandidateDetectionSet? CandidateDetections { get; }
    internal double? LastAcceptedOsdMeter { get; set; }
    internal double? LastAcceptedOsdTimestampSeconds { get; set; }
    internal double CurrentMeter { get; set; }
    internal TimeSpan CurrentVideoTime { get; set; }
    internal byte[]? LastAnalyzedFrame { get; set; }
    internal ReplayVideoFrame? LastAnalyzedVideoFrame { get; set; }

    internal static ReplayCodingState CreateSingle(CodingReplayFrame frame)
        => Create(frame.Id, videoPath: null, frame.DiameterMm!.Value, frame.ReachLengthM!.Value, null, false, null);

    internal static ReplayCodingState CreateVideo(
        ReplayVideoAnalysisContext context,
        ReplayCandidateDetectionSet? candidateDetections = null)
        => Create(context.Holding, context.VideoPath, context.DiameterMm, context.ReachLengthM,
            context, true, candidateDetections);

    private static ReplayCodingState Create(
        string holdingName,
        string? videoPath,
        int diameterMm,
        double reachLengthM,
        ReplayVideoAnalysisContext? video,
        bool useFrameReadiness,
        ReplayCandidateDetectionSet? candidateDetections)
    {
        var session = new CodingSessionService(trainingSamples: new ReplayClosedTrainingStore());
        var holding = new HaltungRecord();
        holding.SetFieldValue("Haltungsname", holdingName, FieldSource.Manual, userEdited: false);
        holding.SetFieldValue("Haltungslaenge_m", reachLengthM.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, userEdited: false);
        holding.SetFieldValue("DN_mm", diameterMm.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, userEdited: false);
        session.StartSession(holding, videoPath);
        return new ReplayCodingState(session, new CodingStreckenschadenTrackerOwner(), video,
            useFrameReadiness, candidateDetections);
    }

    internal IReadOnlyList<ReplayVideoEventSnapshot> SnapshotEvents()
        => Session.Events.Select(ToSnapshot).ToArray();

    internal string? AttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        ReplayVideoFrame? frame,
        byte[]? frameBytes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(Video?.EvidencePhotoRoot))
            return null;
        if (frame is null || frameBytes is null || frameBytes.Length == 0)
            throw new InvalidDataException("Aktueller Replay-Frame fuer den Ereignisbeleg fehlt.");

        var actualSha256 = ReplayFiles.Hash(frameBytes);
        if (!actualSha256.Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Ereignisbeleg {frame.Id} stimmt nicht mit der Frame-Pruefsumme ueberein.");

        var key = frame.Id + ":" + actualSha256;
        if (!_frameEvidencePaths.TryGetValue(key, out var photoPath))
        {
            var root = ReplayFiles.SafePath(Video.EvidencePhotoRoot);
            Directory.CreateDirectory(root);
            photoPath = Path.Combine(root, SafeFrameId(frame.Id) + "_" + actualSha256 + ".png");
            ReplayFiles.WriteNew(photoPath, frameBytes);
            if (!ReplayFiles.HashFile(photoPath).Equals(actualSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Geschriebener Ereignisbeleg {frame.Id} ist nicht bytegleich.");
            _frameEvidencePaths.Add(key, photoPath);
        }

        entry.FotoPaths ??= [];
        entry.OriginalFotoPaths ??= [];
        if (!entry.FotoPaths.Contains(photoPath, StringComparer.OrdinalIgnoreCase))
            entry.FotoPaths.Add(photoPath);
        if (!entry.OriginalFotoPaths.Contains(photoPath, StringComparer.OrdinalIgnoreCase))
            entry.OriginalFotoPaths.Add(photoPath);
        return photoPath;
    }

    internal void RememberNewOrigins(
        IReadOnlyDictionary<Guid, ReplayVideoEventSnapshot> before,
        string meterSource,
        string? frameId = null,
        string? imageSha256 = null)
    {
        foreach (var ev in Session.Events)
        {
            var hasPrevious = before.TryGetValue(ev.EventId, out var previous);
            var captureChanged = hasPrevious
                && (previous!.MeterAtCapture != ev.MeterAtCapture
                    || previous.VideoTimestampSeconds != ev.VideoTimestamp.TotalSeconds);
            if (hasPrevious && !captureChanged && _eventOrigins.ContainsKey(ev.EventId))
                continue;
            _eventOrigins[ev.EventId] = (meterSource, ResolveOrigin(ev), frameId, imageSha256);
        }
    }

    internal IReadOnlyList<ReplayVideoEventChange> Diff(
        IReadOnlyDictionary<Guid, ReplayVideoEventSnapshot> before)
    {
        var after = SnapshotEvents().ToDictionary(e => e.EventId);
        var changes = new List<ReplayVideoEventChange>();
        foreach (var current in after.Values)
        {
            if (!before.TryGetValue(current.EventId, out var previous))
            {
                changes.Add(new ReplayVideoEventChange(ReplayVideoEventChangeKind.Added, current));
                continue;
            }
            if (SnapshotsEqual(previous, current))
                continue;
            var kind = previous.MeterEnd is null && current.MeterEnd is not null
                ? ReplayVideoEventChangeKind.Closed
                : ReplayVideoEventChangeKind.Updated;
            changes.Add(new ReplayVideoEventChange(kind, current));
        }
        foreach (var previous in before.Values.Where(e => !after.ContainsKey(e.EventId)))
            changes.Add(new ReplayVideoEventChange(ReplayVideoEventChangeKind.Removed, previous));
        return changes;
    }

    private static bool SnapshotsEqual(
        ReplayVideoEventSnapshot left,
        ReplayVideoEventSnapshot right)
        => left.EventId == right.EventId
           && left.EntryId == right.EntryId
           && left.Code == right.Code
           && left.MeterStart == right.MeterStart
           && left.MeterEnd == right.MeterEnd
           && left.MeterAtCapture == right.MeterAtCapture
           && left.VideoTimestampSeconds == right.VideoTimestampSeconds
           && left.Source == right.Source
           && left.ReviewState == right.ReviewState
           && left.QualityGate == right.QualityGate
           && left.MeterSource == right.MeterSource
           && left.Origin == right.Origin
           && left.FrameId == right.FrameId
           && left.ImageSha256 == right.ImageSha256
           && left.Evidence == right.Evidence
           && left.HumanTouchedAtUtc == right.HumanTouchedAtUtc
           && left.PhotoPaths.SequenceEqual(right.PhotoPaths, StringComparer.Ordinal)
           && left.OriginalPhotoPaths.SequenceEqual(right.OriginalPhotoPaths, StringComparer.Ordinal)
           && PreviousEvidenceEqual(left.PreviousEvidence, right.PreviousEvidence);

    private static bool PreviousEvidenceEqual(
        IReadOnlyList<ReplayVideoPreviousEvidenceSnapshot> left,
        IReadOnlyList<ReplayVideoPreviousEvidenceSnapshot> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            var a = left[index];
            var b = right[index];
            if (a.Revision != b.Revision
                || a.FrameId != b.FrameId
                || a.ImageSha256 != b.ImageSha256
                || a.EntryId != b.EntryId
                || a.Code != b.Code
                || a.MeterStart != b.MeterStart
                || a.MeterEnd != b.MeterEnd
                || a.MeterAtCapture != b.MeterAtCapture
                || a.VideoTimestampSeconds != b.VideoTimestampSeconds
                || a.HumanTouchedAtUtc != b.HumanTouchedAtUtc
                || a.YoloConfidence != b.YoloConfidence
                || a.DinoConfidence != b.DinoConfidence
                || a.SamConfidence != b.SamConfidence
                || a.SamMaskRleSha256 != b.SamMaskRleSha256
                || a.SamMaskImageWidth != b.SamMaskImageWidth
                || a.SamMaskImageHeight != b.SamMaskImageHeight
                || a.SuggestedByModelId != b.SuggestedByModelId
                || a.SuggestedByModelSha256 != b.SuggestedByModelSha256
                || a.DetectorSource != b.DetectorSource
                || a.DetectorArtifactSha256 != b.DetectorArtifactSha256
                || a.DetectorPurpose != b.DetectorPurpose
                || !a.PhotoPaths.SequenceEqual(b.PhotoPaths, StringComparer.Ordinal)
                || !a.OriginalPhotoPaths.SequenceEqual(b.OriginalPhotoPaths, StringComparer.Ordinal))
                return false;
        }
        return true;
    }

    private ReplayVideoEventSnapshot ToSnapshot(CodingEvent ev)
    {
        var origin = _eventOrigins.GetValueOrDefault(ev.EventId);
        var ai = ev.AiContext;
        var detectorPurpose = CodeMeta(ev, "ai.detector.purpose");
        return new ReplayVideoEventSnapshot(
            ev.EventId,
            ev.Entry.EntryId,
            ev.Entry.Code ?? "",
            ev.Entry.MeterStart,
            ev.Entry.MeterEnd,
            ev.MeterAtCapture,
            ev.VideoTimestamp.TotalSeconds,
            ev.Entry.Source.ToString(),
            ai?.Decision.ToString() ?? ev.ReviewContext?.Decision.ToString() ?? "Unreviewed",
            ai?.QualityGateLevel,
            string.IsNullOrWhiteSpace(origin.MeterSource) ? ResolveStoredMeterSource(ev) : origin.MeterSource,
            string.IsNullOrWhiteSpace(origin.Origin) ? ResolveOrigin(ev) : origin.Origin,
            origin.FrameId,
            origin.ImageSha256,
            new ReplayVideoEventEvidence(
                ai?.Evidence?.YoloConf,
                ai?.Evidence?.DinoConf,
                ai?.Evidence?.SamMaskStability,
                HashMask(ai?.SamMaskRle),
                ai?.SamMaskImageWidth,
                ai?.SamMaskImageHeight,
                CodeMeta(ev, "ai.detector.source"),
                CodeMeta(ev, "ai.detector.sha256"),
                detectorPurpose,
                detectorPurpose == "development_candidate"
                    ? "development_candidate_unqualified"
                    : null,
                ai?.SuggestedByModelId,
                ai?.SuggestedByModelSha256,
                ai?.Reason),
            ev.Entry.FotoPaths?.ToArray() ?? [],
            ev.Entry.OriginalFotoPaths?.ToArray() ?? [],
            ai?.HumanTouchedAtUtc,
            ai?.PreviousEvidence.Select((previous, index) => ToPreviousSnapshot(previous, index + 1)).ToArray()
                ?? []);
    }

    private static ReplayVideoPreviousEvidenceSnapshot ToPreviousSnapshot(
        CodingProposalEvidenceSnapshot previous,
        int revision)
    {
        var identity = ResolveEvidenceIdentity(previous.Entry.FotoPaths);
        return new(
            revision,
            identity.FrameId,
            identity.ImageSha256,
            previous.Entry.EntryId,
            previous.Entry.Code ?? "",
            previous.Entry.MeterStart,
            previous.Entry.MeterEnd,
            previous.MeterAtCapture,
            previous.VideoTimestamp.TotalSeconds,
            previous.Entry.FotoPaths?.ToArray() ?? [],
            previous.Entry.OriginalFotoPaths?.ToArray() ?? [],
            previous.AiContext.HumanTouchedAtUtc,
            previous.AiContext.Evidence?.YoloConf,
            previous.AiContext.Evidence?.DinoConf,
            previous.AiContext.Evidence?.SamMaskStability,
            HashMask(previous.AiContext.SamMaskRle),
            previous.AiContext.SamMaskImageWidth,
            previous.AiContext.SamMaskImageHeight,
            previous.AiContext.SuggestedByModelId,
            previous.AiContext.SuggestedByModelSha256,
            CodeMeta(previous.Entry, "ai.detector.source"),
            CodeMeta(previous.Entry, "ai.detector.sha256"),
            CodeMeta(previous.Entry, "ai.detector.purpose"));
    }

    private static (string? FrameId, string? ImageSha256) ResolveEvidenceIdentity(
        IReadOnlyList<string>? photoPaths)
    {
        foreach (var photoPath in photoPaths ?? [])
        {
            var name = Path.GetFileNameWithoutExtension(photoPath);
            var separator = name.LastIndexOf('_');
            if (separator <= 0 || separator + 65 != name.Length)
                continue;
            var sha256 = name[(separator + 1)..];
            if (sha256.All(Uri.IsHexDigit))
                return (name[..separator], sha256.ToLowerInvariant());
        }
        return (null, null);
    }

    private static string ResolveStoredMeterSource(CodingEvent ev)
        => ev.Entry.CodeMeta?.Parameters.TryGetValue("vsa.meter.quelle", out var value) == true
            ? value
            : "unknown";

    private static string ResolveOrigin(CodingEvent ev)
    {
        var storedSource = CodeMeta(ev, "ai.detector.source");
        if (!string.IsNullOrWhiteSpace(storedSource))
            return storedSource;
        var reason = ev.AiContext?.Reason ?? "";
        if (reason.Contains("Rohranfang", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("Rohrende", StringComparison.OrdinalIgnoreCase))
            return "classifier_boundary";
        if (reason.Contains("DINO", StringComparison.OrdinalIgnoreCase)
            || ev.AiContext?.Evidence?.DinoConf is not null)
            return "dino_sam";
        if (ev.AiContext?.Evidence?.YoloConf is not null)
            return "qualified_yolo_evidence";
        if (reason.Contains("Streckenschaden", StringComparison.OrdinalIgnoreCase))
            return "temporal_stretch_tracker";
        return "unknown";
    }

    private static string? CodeMeta(CodingEvent ev, string key)
        => CodeMeta(ev.Entry, key);

    private static string? HashMask(string? maskRle)
        => string.IsNullOrWhiteSpace(maskRle)
            ? null
            : ReplayFiles.Hash(Encoding.UTF8.GetBytes(maskRle));

    private static string? CodeMeta(ProtocolEntry entry, string key)
        => entry.CodeMeta?.Parameters.TryGetValue(key, out var value) == true
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

    private static string SafeFrameId(string frameId)
    {
        var safe = frameId.Trim();
        foreach (var character in Path.GetInvalidFileNameChars())
            safe = safe.Replace(character, '_');
        return string.IsNullOrWhiteSpace(safe) ? "frame" : safe;
    }
}
