using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Pruefplatz-Orchestrator (Etappe 1): buendelt SAM-Segmentierung, KI-Codevorschlag und das
/// geschuetzte Speichern (Eval-Schutz → TrainingSample → KB-Index → Teacher-Kandidat).
/// Ein Service fuer Center und Player. Implementierung liegt bewusst in der UI-Schicht
/// (wie <see cref="TrainingReviewSamSegmentationService"/>), damit die Application-Schicht keine
/// Infrastruktur bindet.
/// </summary>
public sealed class AnnotationWorkbenchService : IAnnotationWorkbenchService
{
    private readonly ITrainingReviewSamSegmentationService _samService;
    private readonly IVisionPipelineClient _pipelineClient;
    private readonly IRetrievalService? _retrieval;
    private readonly ITrainingSampleStore _sampleStore;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly ITeacherAnnotationStore _teacherStore;
    private readonly IVsaYoloClassMapStore _teacherClassMap;
    private readonly Func<string, byte[]> _readFileBytes;
    private readonly Func<string?> _resolveEvalSetRoot;
    private readonly Func<ITrainingAnnotationExportService>? _exportServiceFactory;

    public AnnotationWorkbenchService(
        ITrainingReviewSamSegmentationService samService,
        IVisionPipelineClient pipelineClient,
        IRetrievalService? retrieval,
        ITrainingSampleStore sampleStore,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore,
        IVsaYoloClassMapStore teacherClassMap,
        Func<string, byte[]> readFileBytes,
        Func<string?> resolveEvalSetRoot,
        Func<ITrainingAnnotationExportService>? exportServiceFactory = null)
    {
        _samService = samService;
        _pipelineClient = pipelineClient;
        _retrieval = retrieval;
        _sampleStore = sampleStore;
        _kbIndexer = kbIndexer;
        _teacherStore = teacherStore;
        _teacherClassMap = teacherClassMap;
        _readFileBytes = readFileBytes;
        _resolveEvalSetRoot = resolveEvalSetRoot;
        _exportServiceFactory = exportServiceFactory;
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
                StatusText: "Keine verwertbare Maske — bitte Box pruefen.",
                Degraded: true);
        }

        double? areaPercent = mask.ImageAreaPixels > 0
            ? Math.Round(100.0 * mask.MaskAreaPixels / mask.ImageAreaPixels, 1)
            : null;

        var statusText = degraded ? "Teil-Segmentierung — pruefen." : "Maske erstellt.";
        return new WorkbenchSegmentation(
            MaskRle: mask.MaskRle,
            MaskImageWidth: resp.ImageWidth,
            MaskImageHeight: resp.ImageHeight,
            AreaPercent: areaPercent,
            StatusText: statusText,
            Degraded: degraded);
    }

    public async Task<WorkbenchSuggestion> SuggestAsync(WorkbenchItem item, BoundingBox box, CancellationToken ct = default)
    {
        // Whole-Frame-Klassifikation (wie produktiv ueblich): Bytes → Base64 → cls.
        var bytes = _readFileBytes(item.FramePath);
        var b64 = Convert.ToBase64String(bytes);
        var resp = await _pipelineClient
            .ClassifyYoloAsync(new YoloClassifyRequest(b64, 5), ct)
            .ConfigureAwait(false);

        var candidates = new List<WorkbenchCodeCandidate>();
        foreach (var p in resp.Predictions)
            candidates.Add(new WorkbenchCodeCandidate(p.ClassName, p.Confidence, "cls"));

        // Aehnliche gepruefte KB-Faelle als zusaetzliche Kandidaten (nur wenn Retrieval verfuegbar).
        if (_retrieval is not null)
        {
            var topCode = candidates.Count > 0 ? candidates[0].VsaCode : null;
            if (!string.IsNullOrWhiteSpace(topCode))
            {
                var hits = await _retrieval.RetrieveAsync(topCode, 3, ct).ConfigureAwait(false);
                foreach (var h in hits)
                    candidates.Add(new WorkbenchCodeCandidate(h.Sample.VsaCode, h.Score, "kb"));
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

    public Task<WorkbenchSaveResult> SaveAsync(WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default)
        => throw new NotImplementedException();
}
