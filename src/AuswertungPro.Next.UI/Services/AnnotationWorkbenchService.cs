using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai;         // VsaCodeResolver (Default-Code-Pruefung)
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Teacher;             // TrainingAnnotationExportServiceFactory (Default)

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Pruefplatz-Orchestrator (Etappe 1): buendelt SAM-Segmentierung, KI-Codevorschlag und das
/// geschuetzte Speichern (Eval-Schutz → Goldkopie → TrainingSample → KB-Index → Teacher-Kandidat).
/// Ein Service fuer Center und Player. Implementierung liegt bewusst in der UI-Schicht
/// (wie <see cref="TrainingReviewSamSegmentationService"/>), damit die Application-Schicht keine
/// Infrastruktur bindet.
/// </summary>
public sealed partial class AnnotationWorkbenchService : IAnnotationWorkbenchService, IDisposable
{
    private readonly ITrainingReviewSamSegmentationService _samService;
    private readonly IVisionPipelineClient _pipelineClient;
    private readonly IRetrievalService? _retrieval;
    private readonly ITrainingSampleStore _sampleStore;
    private readonly ITrainingFrameStore _frameStore;
    private readonly Func<string?> _resolveGoldFramesDir;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly ITeacherAnnotationStore _teacherStore;
    private readonly IVsaYoloClassMapStore _teacherClassMap;
    private readonly Func<string, byte[]> _readFileBytes;
    private readonly Func<string?> _resolveEvalSetRoot;
    private readonly Func<ITrainingAnnotationExportService>? _exportServiceFactory;
    private readonly Func<string, bool> _isCodeKnown;
    private readonly IBcaFineCodeClassifier? _bcaClassifier;
    private readonly Func<string, string?> _codeLabelLookup;
    private readonly IProtocolAiService? _protocolAi;
    private readonly Func<IReadOnlyList<string>> _resolveAllowedCodes;
    private readonly Func<string, (int Width, int Height)?> _readImageDimensions;

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
        _sampleStore = sampleStore;
        _frameStore = frameStore;
        _resolveGoldFramesDir = resolveGoldFramesDir;
        _kbIndexer = kbIndexer;
        _teacherStore = teacherStore;
        _teacherClassMap = teacherClassMap;
        _readFileBytes = readFileBytes;
        _resolveEvalSetRoot = resolveEvalSetRoot;
        _exportServiceFactory = exportServiceFactory;
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
        _protocolAi = protocolAi;
        _resolveAllowedCodes = resolveAllowedCodes
            ?? (() => VsaCodeResolver.CurrentCatalog?.AllowedCodes() ?? Array.Empty<string>());
        // Speichern darf nur einen exakt auswaehlbaren Code des aktiven Katalogs
        // akzeptieren. LookupLabel ist dafuer ungeeignet, weil es absichtlich auf
        // Hauptcodes zurueckfaellt und dadurch erfundene Untercodes beschriften kann.
        _isCodeKnown = isCodeKnown ?? VsaCodeResolver.IsExactSelectableCode;
        _readImageDimensions = readImageDimensions ?? TrainingImageFileProbe.ReadDimensions;
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

        var statusText = degraded ? "Teil-Segmentierung — pruefen." : "Maske erstellt.";
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
            return UnavailablePhotoSuggestion("Der VSA-Codekatalog ist nicht verfuegbar.");

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
        => SaveCoreAsync(item, box, segmentation, decision, imageSnapshot: null, ct);

    public Task<WorkbenchSaveResult> SaveAsync(
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchSegmentation? segmentation,
        WorkbenchDecision decision,
        WorkbenchImageSnapshot imageSnapshot,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageSnapshot);
        return SaveCoreAsync(item, box, segmentation, decision, imageSnapshot, ct);
    }

    private async Task<WorkbenchSaveResult> SaveCoreAsync(
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchSegmentation? segmentation,
        WorkbenchDecision decision,
        WorkbenchImageSnapshot? imageSnapshot,
        CancellationToken ct)
    {
        // 1) Validierung (VOR jedem Schreiben und vor dem Eval-Guard).
        var beschreibung = decision.Beschreibung?.Trim() ?? string.Empty;
        var confirmedByUser = decision.ConfirmedByUser?.Trim() ?? string.Empty;
        var finalCode = NormalizeCode(decision.VsaCode);
        if (confirmedByUser.Length == 0)
        {
            return new WorkbenchSaveResult(
                false,
                "Persoenliche Bestaetigung fehlt. Ohne Bearbeiter wird kein Goldsample gespeichert.",
                null, "-", null);
        }
        if (beschreibung.Length < 10)
            return new WorkbenchSaveResult(false, "Beschreibung zu kurz (mindestens 10 Zeichen).", null, "-", null);
        if (GoldBeschreibungGuard.IsPlaceholder(beschreibung))
            return new WorkbenchSaveResult(
                false,
                "Bitte die Platzhalter-Beschreibung ersetzen (Lage und Ausmass konkret angeben).",
                null, "-", null);
        if (!_isCodeKnown(finalCode))
            return new WorkbenchSaveResult(false, $"Unbekannter VSA-Code '{decision.VsaCode}'.", null, "-", null);

        var repairsExistingSample = !string.IsNullOrWhiteSpace(item.ExistingSampleId);
        TrainingSample? existingSample = null;
        if (repairsExistingSample)
        {
            try
            {
                var matches = (await _sampleStore.LoadAsync().ConfigureAwait(false))
                    .Where(sample => string.Equals(
                        sample.SampleId,
                        item.ExistingSampleId,
                        StringComparison.Ordinal))
                    .ToList();
                if (matches.Count != 1)
                {
                    return new WorkbenchSaveResult(
                        false,
                        matches.Count == 0
                            ? "Goldsample wurde nicht gespeichert: Der zu reparierende Bestandseintrag wurde nicht gefunden."
                            : "Die Sample-ID ist im Bestand nicht eindeutig. Es wurde nichts gespeichert.",
                        null, "-", null);
                }

                existingSample = matches[0];
                if (item.ExpectedConfirmedAtUtc.HasValue
                    && (!existingSample.ConfirmedAtUtc.HasValue
                        || ToUtc(existingSample.ConfirmedAtUtc.Value)
                           != item.ExpectedConfirmedAtUtc.Value.ToUniversalTime()))
                {
                    return new WorkbenchSaveResult(
                        false,
                        "Goldsample wurde inzwischen in einem anderen Arbeitsablauf geaendert. Bitte die Goldpruefung neu laden.",
                        null, "-", null);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new WorkbenchSaveResult(
                    false,
                    $"Das zu reparierende Goldsample konnte nicht sicher gelesen werden: {ex.Message}",
                    null, "-", null);
            }
        }

        var sourceType = existingSample is null
            ? (item.SourceSuggestion is null
                ? SourceTypeNames.ManualCoding
                : SourceTypeNames.PdfPhoto)
            : existingSample.SourceType;
        var sourceNote = existingSample is null
            ? BuildSourceNote(item.SourceSuggestion)
            : existingSample.Notes ?? string.Empty;
        var sourceReferenceCode = existingSample is null
            ? item.SourceSuggestion?.VsaCode?.Trim()
            : existingSample.SourceReferenceCode;
        var sourceReferenceDescription = existingSample is null
            ? item.SourceSuggestion?.Beschreibung?.Trim()
            : existingSample.SourceReferenceDescription;
        var isPdfPhoto = string.Equals(
            sourceType,
            SourceTypeNames.PdfPhoto,
            StringComparison.OrdinalIgnoreCase);
        var isManualCoding = string.Equals(
            sourceType,
            SourceTypeNames.ManualCoding,
            StringComparison.OrdinalIgnoreCase);
        if (!isPdfPhoto && !isManualCoding)
        {
            return new WorkbenchSaveResult(
                false,
                "Die gespeicherte Herkunft ist nicht als persoenliches Gold zugelassen. Es wurde nichts gespeichert.",
                null, "-", null);
        }
        if (isPdfPhoto
            && (!PdfGoldProvenancePolicy.IsValid(sourceNote)
                || string.IsNullOrWhiteSpace(sourceReferenceCode)
                || string.IsNullOrWhiteSpace(sourceReferenceDescription)))
        {
            return new WorkbenchSaveResult(
                false,
                "PDF-Goldsample kann nicht gespeichert werden: Die Operateurreferenz oder PDF-Pruefspur ist unvollstaendig oder ungueltig.",
                null, "-", null);
        }
        if (existingSample is null
            && item.SourceSuggestion is not null
            && !isPdfPhoto)
        {
            return new WorkbenchSaveResult(
                false,
                "Die PDF-Herkunft konnte nicht eindeutig gebunden werden. Es wurde nichts gespeichert.",
                null, "-", null);
        }

        var codeChanged = repairsExistingSample
            && !string.Equals(
                NormalizeCode(existingSample?.Code ?? item.ExistingCode),
                finalCode,
                StringComparison.OrdinalIgnoreCase);
        var keepsExistingReviewDecision = repairsExistingSample
            && !codeChanged
            && existingSample?.Corrected.HasValue == true
            && (string.Equals(
                    existingSample.MatchLevel,
                    MatchLevelNames.ReviewApproved,
                    StringComparison.Ordinal)
                || string.Equals(
                    existingSample.MatchLevel,
                    MatchLevelNames.ReviewCorrected,
                    StringComparison.Ordinal));
        var wasCorrected = keepsExistingReviewDecision
            ? existingSample!.Corrected!.Value
            : isPdfPhoto
                ? !string.Equals(
                    NormalizeCode(sourceReferenceCode),
                    finalCode,
                    StringComparison.OrdinalIgnoreCase)
                : decision.WasCorrected;
        var matchLevel = keepsExistingReviewDecision
            ? existingSample!.MatchLevel!
            : wasCorrected
                ? MatchLevelNames.ReviewCorrected
                : MatchLevelNames.ReviewApproved;

        // Fuer gebundene Qualitaetspruefungen werden genau die beim Laden geprueften
        // Bildbytes als Snapshot verwendet. Damit koennen weder ein Dateiaustausch
        // noch ein Schreib-/Lese-Rennen die alte Maske mit einem neuen Bild verbinden.
        if (!string.IsNullOrWhiteSpace(item.ExpectedImageSha256))
        {
            try
            {
                imageSnapshot ??= WorkbenchImageSnapshot.Create(
                    _readFileBytes(item.FramePath),
                    Path.GetExtension(item.FramePath));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new WorkbenchSaveResult(
                    false,
                    $"Gebundener Bildstand konnte nicht sicher gelesen werden: {ex.Message}",
                    null, "-", null);
            }

            if (!string.Equals(
                    imageSnapshot.Sha256,
                    item.ExpectedImageSha256.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return new WorkbenchSaveResult(
                    false,
                    "Das Bild wurde seit dem Laden der Goldpruefung geaendert. Bitte die Goldpruefung neu laden.",
                    null, "-", null);
            }
        }

        // 2) Eval-Schutz (hart): kein eingefrorenes Mess-Bild darf ins Training/Retrieval.
        var root = _resolveEvalSetRoot();
        EvalContaminationSets evalSets;
        try
        {
            evalSets = EvalContaminationSetProvider.Load(root);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WorkbenchSaveResult(
                false,
                $"Eval-Schutz nicht verfuegbar: {ex.Message}",
                null, "-", null);
        }
        // Beim Foto-Assistenten ist dies genau eine Arbeitskopie des beim
        // Segmentieren gebundenen Originals. Dieselben Bytes gehen unten an
        // StoreBytesAsync; der veraenderbare Quellpfad wird nicht erneut gelesen.
        var snapshotBytes = imageSnapshot?.CopyImageBytes();
        var verdict = snapshotBytes is null
            ? EvalContaminationGuard.ClassifyForExport(
                evalSets.ImageHashes,
                evalSets.HaltungKeys,
                item.FramePath,
                item.CaseId)
            : EvalContaminationGuard.ClassifyForExport(
                evalSets.ImageHashes,
                evalSets.HaltungKeys,
                snapshotBytes,
                item.CaseId);
        if (verdict != EvalContaminationGuard.ExportContaminationResult.Clean)
        {
            return new WorkbenchSaveResult(
                false,
                $"Eval-Schutz: Bild gehoert zum eingefrorenen Mess-Set ({verdict}). Nicht speicherbar.",
                null, "-", null);
        }

        // 3) Das angenommene Bild zuerst unveraendert ins KI-Brain uebernehmen.
        // Stabile Objekt-ID: ein geladener Bestandssatz (z. B. aus 'Unvollstaendige Goldframes')
        // behaelt seine SampleId — bei gleichem Code als Ergaenzung (MergeOrUpdate), bei
        // geaendertem Code als Ersatz (Loeschen + Neuanlage inkl. KB-/Teacher-Bereinigung,
        // siehe Schritt 6). So entsteht bei einer Codekorrektur kein zweiter Datensatz.
        var sampleId = repairsExistingSample
            ? item.ExistingSampleId!
            : $"wb_{Guid.NewGuid():N}"[..15];
        string? storedFramePath;
        try
        {
            var goldFramesRoot = _resolveGoldFramesDir();
            var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                finalCode,
                _codeLabelLookup);
            var codeFramesDir = string.IsNullOrWhiteSpace(goldFramesRoot)
                ? goldFramesRoot
                : Path.Combine(goldFramesRoot, codeFolder);
            storedFramePath = snapshotBytes is null
                ? await _frameStore
                    .StoreExistingAsync(item.FramePath, codeFramesDir, ct)
                    .ConfigureAwait(false)
                : await _frameStore
                    .StoreBytesAsync(snapshotBytes, imageSnapshot!.Extension, codeFramesDir, ct)
                    .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WorkbenchSaveResult(
                false,
                $"Goldbild konnte nicht sicher gespeichert werden: {ex.Message}",
                null, "-", null);
        }
        if (string.IsNullOrWhiteSpace(storedFramePath))
        {
            return new WorkbenchSaveResult(
                false,
                "Goldbild konnte nicht sicher gespeichert werden.",
                null, "-", null);
        }

        string storedImageSha256;
        try
        {
            storedImageSha256 = imageSnapshot?.Sha256
                ?? Convert.ToHexStringLower(SHA256.HashData(_readFileBytes(storedFramePath)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new WorkbenchSaveResult(
                false,
                $"Goldbild konnte nach dem Speichern nicht bytegenau geprueft werden: {ex.Message}",
                null, "-", null);
        }

        // 4) Entwurf oder Gold? Vollstaendig ist ein Fund nur mit gepruefter SAM-Maske.
        // Die zentrale Pruefung (SamMaskValidator) verlangt mehr als HasSamMask: lesbares,
        // dimensionstreues, nicht-leeres RLE, das zur gezogenen Box passt, und kein
        // Degraded-Ergebnis. Ohne gueltige Maske bleibt das Sample ein Entwurf (Schritt 7).
        var maskDimensionsMatch = false;
        if (segmentation is not null)
        {
            try
            {
                var actualDimensions = _readImageDimensions(storedFramePath);
                maskDimensionsMatch = actualDimensions is { } dimensions
                                      && dimensions.Width == segmentation.MaskImageWidth
                                      && dimensions.Height == segmentation.MaskImageHeight;
            }
            catch
            {
                // Unlesbare oder widerspruechliche Bildmasse duerfen nie Gold ergeben.
                maskDimensionsMatch = false;
            }
        }

        var maskValid = maskDimensionsMatch && SamMaskValidator.IsValid(
            segmentation?.MaskRle,
            segmentation?.MaskImageWidth,
            segmentation?.MaskImageHeight,
            box,
            segmentation?.Degraded ?? false,
            out _);
        int? derivedMaskAreaPixels = null;
        if (maskValid
            && SamMaskFormatValidator.TryGetForegroundPixelCount(
                segmentation?.MaskRle,
                segmentation?.MaskImageWidth,
                segmentation?.MaskImageHeight,
                out var foregroundPixelCount,
                out _))
        {
            derivedMaskAreaPixels = foregroundPixelCount;
        }
        else
        {
            maskValid = false;
        }

        // 5) TrainingSample als geprueften Fund bauen (Feldfolge wie ReviewApprovalService).
        var sample = new TrainingSample
        {
            SampleId = sampleId,
            CaseId = item.CaseId,
            Code = finalCode,
            Beschreibung = beschreibung,
            MeterStart = item.MeterStart,
            MeterEnd = item.MeterEnd,
            MeterIsUnknown = item.MeterIsUnknown,
            Signature = TrainingSample.BuildCanonicalSignature(
                item.CaseId,
                finalCode,
                item.MeterStart,
                item.MeterEnd,
                // Mehrfachobjekt: die Hand-Box gehoert zur Objekt-Identitaet — zwei Befunde
                // mit gleichem Code/Meter, aber verschiedenen Boxen sind verschiedene Objekte.
                box.XCenter,
                box.YCenter,
                box.Width,
                box.Height,
                item.MeterIsUnknown),
            Status = maskValid ? TrainingSampleStatus.Approved : TrainingSampleStatus.Draft,
            HumanConfirmed = true,
            Corrected = wasCorrected,
            ConfirmedByUser = confirmedByUser,
            ConfirmedAtUtc = DateTime.UtcNow,
            QualityGateLevel = maskValid ? "Green" : "Yellow",
            SourceType = sourceType,
            Notes = sourceNote,
            SourceReferenceCode = sourceReferenceCode,
            SourceReferenceDescription = sourceReferenceDescription,
            MatchLevel = matchLevel,
            IsStreckenschaden = existingSample?.IsStreckenschaden ?? item.IsStreckenschaden,
            InspectionDate = existingSample?.InspectionDate
                ?? item.InspectionDate
                ?? item.SourceSuggestion?.InspectionDate,
            FramePath = storedFramePath,
            KbIndexState = KbIndexState.Pending,
        };
        PreserveRepairContext(existingSample, sample);
        ApplyDecisionCodeMeta(sample, finalCode, decision);
        box.ApplyTo(sample);
        // Nur eine gepruefte Maske wird persistiert. Eine abgelehnte Maske (Leermaske, falsche
        // Box, Degraded) bleibt bewusst weg, damit der Entwurf ueber !HasSamMask in der Queue
        // 'Unvollstaendige Goldframes' auftaucht und die Maske dort neu segmentiert wird.
        if (maskValid && segmentation is not null)
        {
            sample.SamMaskRle = segmentation.MaskRle;
            sample.SamMaskImageWidth = segmentation.MaskImageWidth;
            sample.SamMaskImageHeight = segmentation.MaskImageHeight;
            sample.SamMaskAreaPixels = derivedMaskAreaPixels;
            sample.SamMaskConfidence = segmentation.Confidence;
            sample.SamMaskLabel = segmentation.Label;
        }

        // Letzte zentrale Gold-Schranke: Caller-Flags allein duerfen keinen
        // unvollstaendigen Fund zu KB oder Teacher durchreichen.
        var goldEligibility = maskValid
            ? ManualGoldTrainingPolicy.EvaluateForExport(sample, confirmedByUser)
            : new TrainingEligibilityResult(
                false,
                ManualGoldTrainingPolicy.GoldGeometryRequiredReason);
        var goldApproved = maskValid && goldEligibility.IsEligible;
        if (!goldApproved)
        {
            sample.Status = TrainingSampleStatus.Draft;
            sample.QualityGateLevel = "Yellow";
        }

        // 6) Neues Sample speichern, ein geladenes Bestandssample ergaenzen (gleicher Code)
        // oder ersetzen (geaenderter Code: gleiche SampleId, neuer Code/Ordner — der
        // Merge-Schluessel ist die Signatur, die den Code enthaelt; ein Code-Wechsel ist
        // daher Loeschen + Neuanlage inkl. KB-/Teacher-Bereinigung).
        string? replaceWarning = null;
        if (repairsExistingSample && codeChanged)
        {
            try
            {
                replaceWarning = await ReplaceSampleWithChangedCodeAsync(item, sample).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new WorkbenchSaveResult(
                    false,
                    $"Goldsample konnte nicht gespeichert werden: {ex.Message}",
                    null, "-", null);
            }
        }
        else if (repairsExistingSample)
        {
            // Gezielt geladenes, unvollstaendiges Goldsample um Box/Segmentierung ergaenzen.
            // So entsteht beim Nachlabeln kein doppelter Datensatz.
            try
            {
                var replaced = await _sampleStore.ReplaceBySampleIdAsync(sample).ConfigureAwait(false);
                if (!replaced)
                {
                    var added = await _sampleStore.TryAddNewAsync(sample, ct).ConfigureAwait(false);
                    if (!added)
                    {
                        return new WorkbenchSaveResult(
                            false,
                            "Goldsample wurde nicht gespeichert: Die Signatur gehoert bereits zu einem anderen Datensatz.",
                            null, "-", null);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new WorkbenchSaveResult(
                    false,
                    $"Goldsample konnte nicht gespeichert werden: {ex.Message}",
                    null, "-", null);
            }

            // Auch ein Nachlabeln mit gleichem Code ersetzt die fachliche Wahrheit
            // (neue Box/Signatur). Alte KB-/Teacher-Ableitungen derselben SampleId
            // muessen deshalb vor dem Neuaufbau entfernt werden.
            replaceWarning = await ReplaceSampleWithChangedCodeAsync(
                    item,
                    sample,
                    sampleAlreadyReplaced: true)
                .ConfigureAwait(false);
        }
        else
        {
            // Neuanlage mit eindeutigem Ergebnis: bei Signatur-Dublett NICHT still
            // weiterlaufen — sonst entstuenden KB-/Teacher-Eintraege ohne JSON-Sample
            // (Waisen). Die inhaltsadressierte Goldkopie ist bei echten Duplikaten
            // ohnehin dieselbe Datei (kein Muell).
            var added = await _sampleStore.TryAddNewAsync(sample, ct).ConfigureAwait(false);
            if (!added)
            {
                return new WorkbenchSaveResult(
                    false,
                    "Bereits als Goldsample vorhanden (gleiche Haltung, Code, Meter und Box). Zum Aendern den Eintrag ueber 'Unvollstaendige Goldframes' oder das Goldalbum laden.",
                    null, "-", null);
            }
        }

        // 7) Entwurf ohne gepruefte Maske: gespeichert, aber NICHT Gold (Status=Draft).
        // KB-Index (KbIndexState bleibt Pending) und Teacher-Kandidat werden NICHT geschrieben.
        // Das Sample landet in 'Unvollstaendige Goldframes'; beim Nachruesten mit Maske
        // (Reparatur ueber denselben SaveAsync) laeuft der volle Gold-Pfad inkl. KB/Teacher.
        if (!goldApproved)
        {
            return new WorkbenchSaveResult(
                true,
                CombineWarnings(
                    "Entwurf gespeichert: ohne gepruefte SAM-Maske kein Goldsample. Das Sample landet in 'Unvollstaendige Goldframes' und kann dort mit Maske nachgeruestet werden.",
                    replaceWarning),
                sampleId,
                "Entwurf",
                null,
                StoredImageSha256: storedImageSha256,
                StoredConfirmedAtUtc: sample.ConfirmedAtUtc is { } draftConfirmedAtUtc
                    ? ToUtc(draftConfirmedAtUtc) : null);
        }

        // 8) KB-Index; Zustand nachtragen (Skipped/Error werden nicht wiederholt).
        // Das Sample ist ab Schritt 6 dauerhaft gespeichert. Ein KB-Index- oder
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

        // 9) Teacher-Kandidat. Ein Teacher-Fehler darf das gespeicherte Sample NICHT ruecknehmen.
        string? teacherId = null;
        string? teacherWarning = null;
        try
        {
            var classId = _teacherClassMap.GetOrAddClassId(finalCode);
            var bbox = new NormalizedBoundingBox
            {
                XCenter = box.XCenter,
                YCenter = box.YCenter,
                Width = box.Width,
                Height = box.Height,
            };
            var annotation = new TeacherAnnotation
            {
                VsaCode = finalCode,
                Beschreibung = beschreibung,
                Severity = decision.Severity,
                MeterPosition = item.MeterStart,
                BoundingBox = bbox,
                ClockPosition = decision.ClockPosition,
                HaltungName = item.HaltungName,   // <-- schliesst die QuarantineOrigin-Luecke
                VideoPath = item.VideoPath,
                SourceSampleId = sampleId,        // <-- Fremdschluessel fuer die Codekorrektur-Bereinigung
            };

            var exportService = _exportServiceFactory?.Invoke()
                ?? TrainingAnnotationExportServiceFactory.Create(_teacherStore);
            var export = await exportService
                .ExportAsync(storedFramePath, bbox, finalCode, classId, $"wb_{annotation.AnnotationId}", ct)
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

        // KB-, Teacher- und Ersetz-Warnung gemeinsam sichtbar machen; das Sample selbst ist gespeichert.
        var warning = CombineWarnings(replaceWarning, kbWarning, teacherWarning);
        return new WorkbenchSaveResult(
            true,
            warning,
            sampleId,
            kbState,
            teacherId,
            GoldApproved: true,
            StoredImageSha256: storedImageSha256,
            StoredConfirmedAtUtc: sample.ConfirmedAtUtc is { } goldConfirmedAtUtc
                ? ToUtc(goldConfirmedAtUtc) : null);
    }

    /// <summary>
    /// Ersetzt ein Bestandssample bei geaenderter Code-Entscheidung (gleiche SampleId, neuer
    /// Code/Ordner): alten Eintrag loeschen, neuen anhaengen, danach den alten KB-Eintrag und
    /// den alten Teacher-Kandidaten entfernen. Das KB-Deindex liegt bewusst VOR dem neuen
    /// Index (Schritt 8), damit nicht der frisch geschriebene Eintrag geloescht wird.
    /// Fehler bei den Bereinigungen machen den Save NICHT rueckgaengig — sie werden als
    /// sichtbare Warnung zurueckgegeben (Muster wie KB-/Teacher-Warnung, nie still).
    /// </summary>
    private async Task<string?> ReplaceSampleWithChangedCodeAsync(
        WorkbenchItem item,
        TrainingSample sample,
        bool sampleAlreadyReplaced = false)
    {
        // Atomares Ersetzen unter einer Sperre (Loeschen + Anhaengen + Speichern in einem
        // Schritt). Existiert die Id nicht (z. B. zwischenzeitlich geloescht), wird der Fund
        // als Neuanlage zusammengefuehrt, damit er nicht verloren geht.
        if (!sampleAlreadyReplaced)
        {
            var replaced = await _sampleStore.ReplaceBySampleIdAsync(sample).ConfigureAwait(false);
            if (!replaced)
            {
                var added = await _sampleStore.TryAddNewAsync(sample).ConfigureAwait(false);
                if (!added)
                {
                    throw new InvalidOperationException(
                        "Die Signatur gehoert bereits zu einem anderen Gold-Datensatz.");
                }
            }
        }

        // KB: alten Code-Eintrag entfernen (gleiche SampleId, alter Code-Inhalt).
        string? warning = null;
        try
        {
            _kbIndexer.Deindex(sample.SampleId);
        }
        catch (Exception ex)
        {
            warning = $"Alter KB-Eintrag konnte nicht entfernt werden: {ex.Message}";
        }

        // Teacher: alten Kandidaten entfernen — sonst lernt der Export weiter den alten Code.
        // Primaertreffer ueber den Fremdschluessel SourceSampleId (Neubestand). Altbestand
        // ohne SourceSampleId: ueber Goldpfad (item.FramePath ist beim Reparatur-Laden der
        // gespeicherte Goldpfad) oder fachliche Signatur (alter Code + Meter + Haltung) —
        // aber NUR bei GENAU EINEM Kandidaten; bei Mehrdeutigkeit nichts loeschen, sondern
        // sichtbar warnen (nie still das Falsche entfernen).
        try
        {
            var oldHaltung = item.HaltungName ?? item.CaseId;
            var candidates = await _teacherStore.LoadAsync().ConfigureAwait(false);
            var stale = candidates
                .Where(annotation =>
                    string.Equals(annotation.SourceSampleId, sample.SampleId, StringComparison.Ordinal))
                .ToList();

            var legacy = candidates
                .Where(annotation => annotation.SourceSampleId is null)
                .Where(annotation =>
                    PathsEqual(annotation.FullFramePath, item.FramePath)
                    || (string.Equals(annotation.VsaCode, item.ExistingCode, StringComparison.OrdinalIgnoreCase)
                        && annotation.MeterPosition == item.MeterStart
                        && string.Equals(annotation.HaltungName, oldHaltung, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (legacy.Count == 1)
                stale.AddRange(legacy);
            else if (legacy.Count > 1)
                warning = CombineWarnings(
                    warning,
                    $"{legacy.Count} alte Teacher-Eintraege unklar zugeordnet — bitte manuell pruefen.");

            foreach (var annotation in stale)
                await _teacherStore.DeleteAsync(annotation.AnnotationId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            warning = CombineWarnings(
                warning,
                $"Alter Teacher-Eintrag konnte nicht entfernt werden: {ex.Message}");
        }

        return warning;
    }

    // Der Pruefplatz baut SAM-Service und Vision-Client pro Fenster frisch (eigener HttpClient).
    // Dispose gibt sie frei, falls disposbar — Fakes/geteilte Clients (nicht IDisposable) bleiben
    // unberuehrt (as IDisposable == null). Wird vom TrainingStudioViewModel beim Schliessen gerufen.
    public void Dispose()
    {
        (_samService as IDisposable)?.Dispose();
        (_pipelineClient as IDisposable)?.Dispose();
        (_bcaClassifier as IDisposable)?.Dispose();
    }
}
