using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai;         // VsaCodeResolver (Default-Code-Pruefung)
using AuswertungPro.Next.UI.Ai.Teacher;             // TrainingAnnotationExportServiceFactory (Default)

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
    private readonly Func<string, bool> _isCodeKnown;

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
        Func<ITrainingAnnotationExportService>? exportServiceFactory = null,
        Func<string, bool>? isCodeKnown = null)
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
        // Default: zentrale VSA-Katalog-Pruefung. Testbar per Delegate, ohne statischen Katalog-Zustand.
        _isCodeKnown = isCodeKnown ?? (code => VsaCodeResolver.LookupLabel(code) is not null);
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

    public async Task<WorkbenchSaveResult> SaveAsync(
        WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default)
    {
        // 1) Validierung (VOR jedem Schreiben und vor dem Eval-Guard).
        var beschreibung = decision.Beschreibung?.Trim() ?? string.Empty;
        if (beschreibung.Length < 10)
            return new WorkbenchSaveResult(false, "Beschreibung zu kurz (mindestens 10 Zeichen).", null, "-", null);
        if (!_isCodeKnown(decision.VsaCode))
            return new WorkbenchSaveResult(false, $"Unbekannter VSA-Code '{decision.VsaCode}'.", null, "-", null);

        // 2) Eval-Schutz (hart): kein eingefrorenes Mess-Bild darf ins Training/Retrieval.
        var root = _resolveEvalSetRoot();
        var evalHashes = EvalContaminationGuard.LoadEvalImageHashes(root);
        var evalHaltungen = EvalContaminationGuard.LoadEvalHaltungKeys(root);
        var verdict = EvalContaminationGuard.ClassifyForExport(evalHashes, evalHaltungen, item.FramePath, item.CaseId);
        if (verdict != EvalContaminationGuard.ExportContaminationResult.Clean)
        {
            return new WorkbenchSaveResult(
                false,
                $"Eval-Schutz: Bild gehoert zum eingefrorenen Mess-Set ({verdict}). Nicht speicherbar.",
                null, "-", null);
        }

        // 3) TrainingSample als geprueften Gold-Fund bauen (Feldfolge wie ReviewApprovalService).
        var sampleId = $"wb_{Guid.NewGuid():N}"[..15];
        var sample = new TrainingSample
        {
            SampleId = sampleId,
            CaseId = item.CaseId,
            Code = decision.VsaCode,
            Beschreibung = beschreibung,
            MeterStart = item.MeterStart,
            MeterEnd = item.MeterEnd,
            Signature = TrainingSample.BuildCanonicalSignature(item.CaseId, decision.VsaCode, item.MeterStart, item.MeterEnd),
            Status = TrainingSampleStatus.Approved,
            HumanConfirmed = true,
            Corrected = decision.WasCorrected,
            ConfirmedByUser = decision.ConfirmedByUser,
            ConfirmedAtUtc = DateTime.UtcNow,
            QualityGateLevel = "Green",
            SourceType = SourceTypeNames.ManualCoding,
            MatchLevel = decision.WasCorrected ? MatchLevelNames.ReviewCorrected : MatchLevelNames.ReviewApproved,
            FramePath = item.FramePath,
            KbIndexState = KbIndexState.Pending,
        };
        box.ApplyTo(sample);
        if (segmentation is not null && !string.IsNullOrEmpty(segmentation.MaskRle))
        {
            sample.SamMaskRle = segmentation.MaskRle;
            sample.SamMaskImageWidth = segmentation.MaskImageWidth;
            sample.SamMaskImageHeight = segmentation.MaskImageHeight;
        }

        // 4) Neues Sample speichern (Dedup via Signatur, kein Ueberschreiben).
        await _sampleStore.MergeAndSaveAsync(new List<TrainingSample> { sample }).ConfigureAwait(false);

        // 5) KB-Index; Zustand nachtragen (Skipped/Error werden nicht wiederholt).
        // Das Sample ist ab Schritt 4 dauerhaft gespeichert. Ein KB-Index- oder
        // Nachtrags-Fehler (SQLite-Lock, DB-Fehler) darf den Save deshalb NICHT als
        // "Nicht gespeichert" darstellen — sonst legt der Nutzer dasselbe Sample erneut an.
        // Wie beim Teacher-Schritt wird der Fehler als sichtbare Warnung zurueckgegeben.
        string kbState;
        string? kbWarning = null;
        try
        {
            var outcome = await _kbIndexer.IndexAsync(new[] { sample }, ct).ConfigureAwait(false);
            sample.KbIndexState = outcome.IsIndexed(sampleId) ? KbIndexState.Indexed
                : outcome.IsSkipped(sampleId) ? KbIndexState.Skipped
                : KbIndexState.Error;
            await _sampleStore.MergeOrUpdateAsync(new List<TrainingSample> { sample }).ConfigureAwait(false);
            kbState = sample.KbIndexState.ToString();
        }
        catch (Exception ex)
        {
            kbState = KbIndexState.Error.ToString();
            kbWarning = $"KB-Index nicht aktualisiert: {ex.Message}";
        }

        // 6) Teacher-Kandidat. Ein Teacher-Fehler darf das gespeicherte Sample NICHT ruecknehmen.
        string? teacherId = null;
        string? teacherWarning = null;
        try
        {
            var classId = _teacherClassMap.GetOrAddClassId(decision.VsaCode);
            var bbox = new NormalizedBoundingBox
            {
                XCenter = box.XCenter,
                YCenter = box.YCenter,
                Width = box.Width,
                Height = box.Height,
            };
            var annotation = new TeacherAnnotation
            {
                VsaCode = decision.VsaCode,
                Beschreibung = beschreibung,
                Severity = decision.Severity,
                MeterPosition = item.MeterStart,
                BoundingBox = bbox,
                ClockPosition = decision.ClockPosition,
                HaltungName = item.HaltungName,   // <-- schliesst die QuarantineOrigin-Luecke
                VideoPath = item.VideoPath,
            };

            var exportService = _exportServiceFactory?.Invoke()
                ?? TrainingAnnotationExportServiceFactory.Create(_teacherStore);
            var export = await exportService
                .ExportAsync(item.FramePath, bbox, decision.VsaCode, classId, $"wb_{annotation.AnnotationId}", ct)
                .ConfigureAwait(false);
            if (!export.Success)
                throw new InvalidOperationException(export.Error ?? "Teacher-Export meldete keinen Erfolg.");

            annotation.FullFramePath = export.FullFramePath;
            annotation.CroppedRegionPath = export.CroppedRegionPath;
            annotation.YoloAnnotationPath = export.YoloAnnotationPath;
            await _teacherStore.AppendAsync(annotation).ConfigureAwait(false);
            teacherId = annotation.AnnotationId;
        }
        catch (Exception ex)
        {
            // Sample bleibt gespeichert; die Warnung wird sichtbar zurueckgegeben (nie still).
            teacherWarning = $"Teacher-Kandidat nicht gespeichert: {ex.Message}";
        }

        // KB- und Teacher-Warnung gemeinsam sichtbar machen; das Sample selbst ist gespeichert.
        var warning = CombineWarnings(kbWarning, teacherWarning);
        return new WorkbenchSaveResult(true, warning, sampleId, kbState, teacherId);
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var present = warnings.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
        return present.Length == 0 ? null : string.Join(" | ", present);
    }
}
