using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Slice 1 (Operateur-Annotation): Two-Phase-Service.
/// Phase 1 — <see cref="PreviewMaskAsync"/> ruft das Sidecar mit
/// <c>return_polygon=true</c> auf, persistiert nichts.
/// Phase 2 — <see cref="CommitAsync"/> finalisiert den Frame und schreibt
/// Best-Effort Store &gt; YOLO &gt; KB.
/// </summary>
public sealed class OperateurAnnotationService : IOperateurAnnotationService
{
    private readonly Func<SamRequest, CancellationToken, Task<SamResponse>> _samDelegate;
    private readonly ITrainingSamplesWriter _writer;
    private readonly IKnowledgeBaseIndexer _indexer;
    private readonly IYoloDatasetWriter _yolo;
    private readonly Func<DateTime> _clock;

    public OperateurAnnotationService(
        Func<SamRequest, CancellationToken, Task<SamResponse>> samDelegate,
        ITrainingSamplesWriter writer,
        IKnowledgeBaseIndexer indexer,
        IYoloDatasetWriter yolo,
        Func<DateTime>? clock = null)
    {
        _samDelegate = samDelegate ?? throw new ArgumentNullException(nameof(samDelegate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _yolo = yolo ?? throw new ArgumentNullException(nameof(yolo));
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public async Task<MaskPreview> PreviewMaskAsync(AnnotationRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.FramePath))
            throw new ArgumentException("AnnotationRequest.FramePath ist Pflicht.", nameof(request));
        if (request.FrameWidth <= 0 || request.FrameHeight <= 0)
            throw new ArgumentException("FrameWidth/FrameHeight muessen > 0 sein.", nameof(request));

        var frameBytes = await File.ReadAllBytesAsync(request.FramePath, ct).ConfigureAwait(false);
        var imageBase64 = Convert.ToBase64String(frameBytes);

        var pixelBox = DenormalizeBoxToPixels(request.Box, request.FrameWidth, request.FrameHeight);

        var samRequest = new SamRequest(
            ImageBase64: imageBase64,
            BoundingBoxes: new[]
            {
                new SamBoundingBox(
                    X1: pixelBox.X1, Y1: pixelBox.Y1,
                    X2: pixelBox.X2, Y2: pixelBox.Y2,
                    Label: "operateur_box",
                    Confidence: 1.0),
            },
            ReturnPolygon: true);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var samResponse = await _samDelegate(samRequest, ct).ConfigureAwait(false);
        stopwatch.Stop();

        if (samResponse.Masks is null || samResponse.Masks.Count == 0)
            throw new InvalidOperationException(
                "SAM hat keine Maske geliefert — Box vermutlich zu klein oder degeneriert.");

        var mask = samResponse.Masks[0];

        var warnings = new List<string>();
        string polygonJson;
        if (mask.PolygonPoints is null || mask.PolygonPoints.Count < 3)
        {
            warnings.Add("PolygonMissing");
            polygonJson = "[]";
        }
        else
        {
            polygonJson = JsonSerializer.Serialize(mask.PolygonPoints);
        }

        return new MaskPreview(
            SamMaskRle: mask.MaskRle,
            SamMaskEncoding: "sidecar-sam-rle-v1",
            PolygonJson: polygonJson,
            MaskWidth: samResponse.ImageWidth,
            MaskHeight: samResponse.ImageHeight,
            MaskAreaPixels: mask.MaskAreaPixels,
            SamConfidence: mask.Confidence,
            SamLatency: stopwatch.Elapsed,
            Warnings: warnings.Count == 0 ? null : warnings);
    }

    public async Task<CommitResult> CommitAsync(AnnotationRequest request, MaskPreview preview, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (preview is null) throw new ArgumentNullException(nameof(preview));
        ct.ThrowIfCancellationRequested();

        var sampleId = Guid.NewGuid().ToString("N");
        var warnings = new List<string>();

        // Step 0: Frame finalisieren — temp-Pfad → KI_BRAIN/frames/<CaseId>/<SampleId>.png.
        // Korrektur K3/K4 (Plan-Header): vorher unklar, jetzt vor jedem Persist-Schritt.
        // CaseId kommt aus PDF-/Ordner-Importen und kann '/', '\', ':', '..' enthalten —
        // SanitizePathSegment neutralisiert das (Path-Traversal-Schutz, identisch zu
        // HoldingFolderDistributor und Co.).
        var brainRoot = KnowledgeRootProvider.GetRoot();
        var safeCaseId = ProjectPathResolver.SanitizePathSegment(request.CaseId);
        var caseDir = Path.Combine(brainRoot, "frames", safeCaseId);
        Directory.CreateDirectory(caseDir);

        var sourceExt = Path.GetExtension(request.FramePath);
        if (string.IsNullOrEmpty(sourceExt)) sourceExt = ".png";
        var finalFramePath = Path.Combine(caseDir, sampleId + sourceExt);

        try
        {
            File.Copy(request.FramePath, finalFramePath, overwrite: false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new CommitResult(
                IsSuccess: false,
                SampleId: sampleId,
                FramePath: null,
                LabelPath: null,
                StorePersisted: false,
                KbIndexed: false,
                YoloWritten: false,
                Error: $"FrameFinalize: {ex.Message}",
                Warnings: null);
        }

        var sample = BuildSample(request, preview, sampleId, finalFramePath);

        // Step 1: Store. Store-Erfolg = Commit-Erfolg.
        try
        {
            await _writer.AppendAsync(sample, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }   // K3
        catch (Exception ex)
        {
            return new CommitResult(
                IsSuccess: false,
                SampleId: sampleId,
                FramePath: finalFramePath,
                LabelPath: null,
                StorePersisted: false,
                KbIndexed: false,
                YoloWritten: false,
                Error: $"Store: {ex.Message}",
                Warnings: null);
        }

        // Step 2: YOLO (best-effort).
        string? labelPath = null;
        bool yoloWritten = false;
        try
        {
            labelPath = await _yolo.AppendSampleAsync(sample, preview, ct).ConfigureAwait(false);
            yoloWritten = true;
        }
        catch (OperationCanceledException) { throw; }   // K3
        catch (Exception ex)
        {
            warnings.Add($"YoloFailed:{ex.Message}");
        }

        // Step 3: KB (best-effort) — K2: Erfolg setzt KbIndexState.Indexed,
        // Fehler setzt KbIndexState.Pending (sodass spaeterer Re-Indexer es findet).
        bool kbIndexed = false;
        try
        {
            await _indexer.IndexSampleAsync(sample, ct).ConfigureAwait(false);
            kbIndexed = true;
            try
            {
                await _writer.UpdateIndexStateAsync(sampleId, KbIndexState.Indexed, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }   // K3
            catch (Exception)
            {
                warnings.Add("KbIndexedStateUpdateFailed");
            }
        }
        catch (OperationCanceledException) { throw; }   // K3
        catch (Exception ex)
        {
            warnings.Add($"KbFailed:{ex.Message}");
            try
            {
                await _writer.UpdateIndexStateAsync(sampleId, KbIndexState.Pending, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }   // K3
            catch (Exception)
            {
                warnings.Add("KbStateUpdateFailed");
            }
        }

        return new CommitResult(
            IsSuccess: true,
            SampleId: sampleId,
            FramePath: finalFramePath,
            LabelPath: labelPath,
            StorePersisted: true,
            KbIndexed: kbIndexed,
            YoloWritten: yoloWritten,
            Error: null,
            Warnings: warnings.Count == 0 ? null : warnings);
    }

    private TrainingSample BuildSample(
        AnnotationRequest request,
        MaskPreview preview,
        string sampleId,
        string finalFramePath)
    {
        var sample = new TrainingSample
        {
            SampleId = sampleId,
            CaseId = request.CaseId,
            Code = request.Code,
            MeterStart = request.ProtocolMeterstand,
            MeterEnd = request.ProtocolMeterstand,
            IsStreckenschaden = false,
            TimeSeconds = request.ActualFrameTimeSeconds,
            FramePath = finalFramePath,
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.OperateurAnnotation,
            FrameIndex = request.VideoFrameIndex,
            FrameDeltaSeconds = request.ActualFrameTimeSeconds - request.SuggestedFrameTimeSeconds,

            // SAM-Maske aus dem Preview (Slice 1).
            SamMaskRle = preview.SamMaskRle,
            SamMaskEncoding = preview.SamMaskEncoding,
            MaskWidth = preview.MaskWidth,
            MaskHeight = preview.MaskHeight,
            MaskAreaPixels = preview.MaskAreaPixels,
            SamConfidence = preview.SamConfidence,

            // BoundingBox in YOLO-Format (Center + Size, normiert) aus der Operateur-Eingabe.
            BboxXCenter = request.Box.XCenter,
            BboxYCenter = request.Box.YCenter,
            BboxWidth = request.Box.Width,
            BboxHeight = request.Box.Height,

            KbIndexState = KbIndexState.None,
        };
        sample.Signature = TrainingSample.BuildCanonicalSignature(
            request.CaseId, request.Code, request.ProtocolMeterstand, request.ProtocolMeterstand);
        return sample;
    }

    /// <summary>
    /// Wandelt eine YOLO-format Box (Center + Size, normiert 0..1) in
    /// Pixelkoordinaten (x1, y1, x2, y2). Mit Clamp gegen Frame-Grenzen.
    /// </summary>
    private static (double X1, double Y1, double X2, double Y2) DenormalizeBoxToPixels(
        BoundingBoxNormalized box, int frameWidth, int frameHeight)
    {
        var halfW = box.Width / 2.0;
        var halfH = box.Height / 2.0;
        var x1 = Math.Clamp((box.XCenter - halfW) * frameWidth, 0.0, frameWidth);
        var y1 = Math.Clamp((box.YCenter - halfH) * frameHeight, 0.0, frameHeight);
        var x2 = Math.Clamp((box.XCenter + halfW) * frameWidth, 0.0, frameWidth);
        var y2 = Math.Clamp((box.YCenter + halfH) * frameHeight, 0.0, frameHeight);
        return (x1, y1, x2, y2);
    }
}
