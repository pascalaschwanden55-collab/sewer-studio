using AuswertungPro.Next.Application.Common;
using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;
using AuswertungPro.Next.Infrastructure.Ai;         // VsaCodeResolver (Default-Code-Pruefung)
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Teacher;             // TrainingAnnotationExportServiceFactory (Default)

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Pruefplatz-Fassade (Etappe 1): buendelt SAM-Segmentierung und KI-Codevorschlag. Das
/// geschuetzte Speichern (Eval-Schutz → Goldkopie → TrainingSample → KB-Index →
/// Teacher-Kandidat) steuert <see cref="GoldSampleSpeichernUseCase"/> in der
/// Application-Schicht; dieser Dienst baut ihn aus seinen Abhaengigkeiten und reicht die
/// Infrastruktur-Schritte (Eval-Schutzdaten laden, strenge Maskenpruefung, Teacher-Export)
/// als Delegates hinein. Ein Service fuer Center und Player.
/// </summary>
public sealed class AnnotationWorkbenchService : IAnnotationWorkbenchService, IDisposable
{
    private readonly ITrainingReviewSamSegmentationService _samService;
    private readonly IVisionPipelineClient _pipelineClient;
    private readonly IRetrievalService? _retrieval;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly Func<string, byte[]> _readFileBytes;
    private readonly Func<string, bool> _isCodeKnown;
    private readonly IBcaFineCodeClassifier? _bcaClassifier;
    private readonly IProtocolAiService? _protocolAi;
    private readonly Func<IReadOnlyList<string>> _resolveAllowedCodes;
    private readonly GoldSampleSpeichernUseCase _goldSampleSpeichern;

    public AnnotationWorkbenchService(
        ITrainingReviewSamSegmentationService samService,
        IVisionPipelineClient pipelineClient,
        IRetrievalService? retrieval,
        ITrainingSampleStore sampleStore,
        ITrainingFrameStore frameStore,
        Func<string?> resolveGoldFramesDir,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore,
        IVsaYoloClassMapStore teacherClassMap,
        Func<string, byte[]> readFileBytes,
        Func<string?> resolveEvalSetRoot,
        Func<ITrainingAnnotationExportService>? exportServiceFactory = null,
        Func<string, bool>? isCodeKnown = null,
        IBcaFineCodeClassifier? bcaClassifier = null,
        Func<string, string?>? codeLabelLookup = null,
        IProtocolAiService? protocolAi = null,
        Func<IReadOnlyList<string>>? resolveAllowedCodes = null,
        Func<string, (int Width, int Height)?>? readImageDimensions = null)
    {
        _bcaClassifier = bcaClassifier;
        _samService = samService;
        _pipelineClient = pipelineClient;
        _retrieval = retrieval;
        _kbIndexer = kbIndexer;
        _readFileBytes = readFileBytes;
        _protocolAi = protocolAi;
        _resolveAllowedCodes = resolveAllowedCodes
            ?? (() => VsaCodeResolver.CurrentCatalog?.AllowedCodes() ?? Array.Empty<string>());
        // Speichern darf nur einen exakt auswaehlbaren Code des aktiven Katalogs
        // akzeptieren. LookupLabel ist dafuer ungeeignet, weil es absichtlich auf
        // Hauptcodes zurueckfaellt und dadurch erfundene Untercodes beschriften kann.
        _isCodeKnown = isCodeKnown ?? VsaCodeResolver.IsExactSelectableCode;
        var dimensionsReader = readImageDimensions ?? TrainingImageFileProbe.ReadDimensions;
        _goldSampleSpeichern = new GoldSampleSpeichernUseCase(
            sampleStore,
            frameStore,
            resolveGoldFramesDir,
            kbIndexer,
            teacherStore,
            teacherClassMap,
            () => exportServiceFactory?.Invoke()
                  ?? TrainingAnnotationExportServiceFactory.Create(teacherStore),
            readFileBytes,
            resolveEvalSetRoot,
            LoadEvalSchutz,
            _isCodeKnown,
            codeLabelLookup ?? VsaCodeResolver.LookupLabel,
            (segmentation, box, storedFramePath) => WorkbenchGoldMask.Evaluate(
                segmentation, box, storedFramePath, dimensionsReader));
    }

    private static GoldSampleEvalSchutz LoadEvalSchutz(string? evalSetRoot)
    {
        var sets = EvalContaminationSetProvider.Load(evalSetRoot);
        return new GoldSampleEvalSchutz(sets.ImageHashes, sets.HaltungKeys);
    }

    public async Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default)
    {
        var result = await _samService
            .SegmentFrameFileAsync(item.FramePath, box, codeHint, item.PipeDiameterMm, ct)
            .ConfigureAwait(false);
        var resp = result.Response;

        // Teil-Segmentierung sobald Boxen verloren gingen oder der Sidecar degraded meldet.
        var degraded = resp.Degraded || resp.SkippedBoxes > 0;

        // Erste Maske mit echtem RLE (Muster TrainingReviewSamWorkflow).
        var mask = resp.Masks.FirstOrDefault(m => !string.IsNullOrEmpty(m.MaskRle));
        if (mask is null)
        {
            return new WorkbenchSegmentation(
                MaskRle: null,
                MaskImageWidth: resp.ImageWidth,
                MaskImageHeight: resp.ImageHeight,
                AreaPercent: null,
                StatusText: "Keine verwertbare Maske — bitte Box prüfen.",
                Degraded: true);
        }

        var maskAreaPixels = SamMaskFormatValidator.TryGetForegroundPixelCount(
            mask.MaskRle,
            resp.ImageWidth,
            resp.ImageHeight,
            out var parsedMaskAreaPixels,
            out _)
            ? parsedMaskAreaPixels
            : (int?)null;
        double? areaPercent = maskAreaPixels.HasValue
                              && resp.ImageWidth > 0
                              && resp.ImageHeight > 0
            ? Math.Round(
                100.0 * maskAreaPixels.Value / (resp.ImageWidth * (double)resp.ImageHeight),
                1)
            : null;

        var statusText = degraded ? "Teil-Segmentierung — prüfen." : "Maske erstellt.";
        return new WorkbenchSegmentation(
            MaskRle: mask.MaskRle,
            MaskImageWidth: resp.ImageWidth,
            MaskImageHeight: resp.ImageHeight,
            AreaPercent: areaPercent,
            StatusText: statusText,
            Degraded: degraded,
            MaskAreaPixels: maskAreaPixels,
            Confidence: mask.Confidence,
            Label: mask.Label);
    }

    public Task<WorkbenchSuggestion> SuggestAsync(
        WorkbenchItem item,
        BoundingBox box,
        CancellationToken ct = default)
        => SuggestWithClassifierAsync(item, ct);

    public async Task<WorkbenchSuggestion> SuggestPhotoAsync(
        WorkbenchItem item,
        CancellationToken ct = default)
    {
        if (_protocolAi is null || _protocolAi is NoopProtocolAiService)
        {
            return UnavailablePhotoSuggestion(
                "Die allgemeine Foto-KI ist deaktiviert oder nicht eingerichtet.");
        }

        var allowedCodes = _resolveAllowedCodes()
            .Select(code => code?.Trim().ToUpperInvariant())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowedCodes.Length == 0)
            return UnavailablePhotoSuggestion("Der VSA-Codekatalog ist nicht verfügbar.");

        var projectFolder = Path.GetDirectoryName(item.FramePath);
        if (string.IsNullOrWhiteSpace(projectFolder))
            projectFolder = AppContext.BaseDirectory;

        var result = await _protocolAi
            .SuggestAsync(
                new AiInput(
                    ProjectFolderAbs: projectFolder,
                    HaltungId: item.HaltungName ?? item.CaseId,
                    Meter: item.MeterStart,
                    ExistingCode: item.ExistingCode,
                    ExistingText: item.ExistingBeschreibung,
                    AllowedCodes: allowedCodes,
                    VideoPathAbs: null,
                    Zeit: null,
                    ImagePathsAbs: new[] { item.FramePath },
                    RequireImage: true),
                ct)
            .ConfigureAwait(false);

        if (result is null)
            return UnavailablePhotoSuggestion("Die allgemeine Foto-KI ist nicht erreichbar.");

        var code = ResolveKnownSuggestionCode(result.SuggestedCode);
        if (code is null
            || !allowedCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return new WorkbenchSuggestion(
                Array.Empty<WorkbenchCodeCandidate>(),
                FrameUsable: true,
                result.Reason ?? string.Empty,
                IsBend: false);
        }

        var source = result.Flags.Contains("kb_fallback", StringComparer.OrdinalIgnoreCase)
            ? "kb"
            : "qwen";
        var candidate = new WorkbenchCodeCandidate(
            code,
            Math.Clamp(result.Confidence, 0, 1),
            source);
        return new WorkbenchSuggestion(
            new[] { candidate },
            FrameUsable: true,
            result.Reason ?? string.Empty,
            IsBend: false);
    }

    private static WorkbenchSuggestion UnavailablePhotoSuggestion(string reason)
        => new(
            Array.Empty<WorkbenchCodeCandidate>(),
            FrameUsable: true,
            QualityReason: string.Empty,
            IsBend: false,
            ModelAvailable: false,
            UnavailableReason: reason);

    private async Task<WorkbenchSuggestion> SuggestWithClassifierAsync(
        WorkbenchItem item,
        CancellationToken ct)
    {
        // Whole-Frame-Klassifikation (wie produktiv ueblich): Bytes → Base64 → cls.
        var bytes = _readFileBytes(item.FramePath);
        var b64 = Convert.ToBase64String(bytes);
        var resp = await _pipelineClient
            .ClassifyYoloAsync(new YoloClassifyRequest(b64, 5), ct)
            .ConfigureAwait(false);

        if (!resp.ClassifierLoaded)
        {
            return new WorkbenchSuggestion(
                Array.Empty<WorkbenchCodeCandidate>(),
                resp.Usable,
                resp.QualityReason,
                resp.IsBend,
                ModelAvailable: false,
                UnavailableReason: "Das Klassifikationsmodell ist nicht geladen.");
        }

        var candidates = new List<WorkbenchCodeCandidate>();
        foreach (var p in resp.Predictions)
        {
            var code = ResolveKnownSuggestionCode(p.ClassName);
            if (code is not null)
                candidates.Add(new WorkbenchCodeCandidate(code, p.Confidence, "cls"));
        }

        // Aehnliche gepruefte KB-Faelle als zusaetzliche Kandidaten (nur wenn Retrieval verfuegbar).
        if (_retrieval is not null)
        {
            var topCode = candidates.Count > 0 ? candidates[0].VsaCode : null;
            if (!string.IsNullOrWhiteSpace(topCode))
            {
                var hits = await _retrieval.RetrieveAsync(topCode, 3, ct).ConfigureAwait(false);
                foreach (var h in hits)
                {
                    var code = ResolveKnownSuggestionCode(h.Sample.VsaCode);
                    if (code is not null)
                        candidates.Add(new WorkbenchCodeCandidate(code, h.Score, "kb"));
                }
            }
        }

        // Gleiche Codes zusammenfassen: hoechste Confidence gewinnt (mitsamt ihrer Quelle),
        // Ergebnis absteigend nach Confidence.
        var deduped = candidates
            .GroupBy(c => c.VsaCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.Confidence).First())
            .OrderByDescending(c => c.Confidence)
            .ToList();

        return new WorkbenchSuggestion(deduped, resp.Usable, resp.QualityReason, resp.IsBend);
    }

    private string? ResolveKnownSuggestionCode(string? rawCode)
    {
        var mapped = YoloClassVsaMapper.ToPersistableVsaCode(rawCode);
        if (!string.IsNullOrWhiteSpace(mapped) && _isCodeKnown(mapped))
            return mapped.ToUpperInvariant();

        var normalized = rawCode?.Trim().ToUpperInvariant();
        return !string.IsNullOrWhiteSpace(normalized) && _isCodeKnown(normalized)
            ? normalized
            : null;
    }

    public bool BcaBauartVerfuegbar => _bcaClassifier is not null;

    public async Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default)
    {
        // Ohne verfuegbaren Qwen-Classifier bleibt der Knopf wirkungslos (kein Fehlerzustand).
        if (_bcaClassifier is null)
            return new WorkbenchSuggestion(Array.Empty<WorkbenchCodeCandidate>(), true, string.Empty, false);

        var b64 = Convert.ToBase64String(_readFileBytes(item.FramePath));
        var suggestion = await _bcaClassifier.SuggestAsync(b64, ct).ConfigureAwait(false);

        // Feine Bauart-Codes als zusaetzliche Kandidaten mit klarer Herkunft "bca".
        var candidates = suggestion.Candidates
            .Select(c => new WorkbenchCodeCandidate(c.VsaCode, c.Confidence, "bca"))
            .ToList();
        return new WorkbenchSuggestion(candidates, true, string.Empty, false);
    }

    public Task<WorkbenchSaveResult> SaveAsync(
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchSegmentation? segmentation,
        WorkbenchDecision decision,
        CancellationToken ct = default)
        => _goldSampleSpeichern.SaveAsync(
            new GoldSampleSpeichernAnfrage(item, box, segmentation, decision),
            ct);

    public Task<WorkbenchSaveResult> SaveAsync(
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchSegmentation? segmentation,
        WorkbenchDecision decision,
        WorkbenchImageSnapshot imageSnapshot,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageSnapshot);
        return _goldSampleSpeichern.SaveAsync(
            new GoldSampleSpeichernAnfrage(item, box, segmentation, decision, imageSnapshot),
            ct);
    }

    // Der Pruefplatz baut SAM-Service und Vision-Client pro Fenster frisch (eigener HttpClient).
    // Dispose gibt sie frei, falls disposbar — Fakes/geteilte Clients (nicht IDisposable) bleiben
    // unberuehrt (as IDisposable == null). Wird vom TrainingStudioViewModel beim Schliessen gerufen.
    public void Dispose()
    {
        (_samService as IDisposable)?.Dispose();
        (_pipelineClient as IDisposable)?.Dispose();
        (_bcaClassifier as IDisposable)?.Dispose();
        (_kbIndexer as IDisposable)?.Dispose();
    }
}
